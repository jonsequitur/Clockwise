using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Pocket;
using StackExchange.Redis;
using static System.String;

namespace Clockwise.Redis
{
    public sealed class CircuitBreakerStorage : ICircuitBreakerStorage, IDisposable,
        IObserver<(string key, string operation)>
    {
        private readonly string connectionString;
        private readonly int dbId;
        private static readonly Logger Log = new Logger("CircuitBreakerStorage");

        private ConnectionMultiplexer connection;
        private IDatabase db;
        private readonly string key;
        private ISubscriber subscriber;
        private static readonly JsonSerializerSettings jsonSettings;
        private readonly ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>> observers;
        private KeySapceObserver keySapceObserver;
        private IDisposable keySpaceSubscription;
        private CircuitBreakerStateDescriptor stateDescriptor;


        private string lastSerialisedState;

        static CircuitBreakerStorage()
        {
            jsonSettings = new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()};
            jsonSettings.Converters.Add(new StringEnumConverter());
            jsonSettings.NullValueHandling = NullValueHandling.Ignore;
        }

        public CircuitBreakerStorage(string connectionString, int dbId, Type resourceType)
        {
            this.connectionString = connectionString;
            this.dbId = dbId;
            observers = new ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>>();
            key = $"{resourceType.Name}.circuitBreaker";
        }
        public async Task Initialise()
        {
            connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            db = connection.GetDatabase(dbId);
            subscriber = connection.GetSubscriber();
            keySapceObserver = new KeySapceObserver(dbId, key, subscriber);
            keySpaceSubscription = keySapceObserver.Subscribe(this);
            await keySapceObserver.Initialise();
        }

        public async Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            var serialised = await db.StringGetAsync(key);
            lastSerialisedState = serialised.HasValue ? serialised.ToString() : Empty;
            if (IsNullOrWhiteSpace(serialised))
            {
                var newState = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now(),
                    TimeSpan.FromMinutes(1));
                lastSerialisedState = await Transistion(lastSerialisedState,
                    JsonConvert.SerializeObject(newState, jsonSettings), null);
            }

            return TryDeserialise(lastSerialisedState);
        }

        private static CircuitBreakerStateDescriptor TryDeserialise(string serialised)
        {
            return IsNullOrWhiteSpace(serialised)
                ? null
                : JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(serialised, jsonSettings);
        }

        public async Task SignalFailureAsync(TimeSpan expiry)
        {
            var openState = new CircuitBreakerStateDescriptor(CircuitBreakerState.Open, Clock.Current.Now(), expiry);
            await TransitionStateTo( openState, expiry);
        }

        public async Task SignalSuccessAsync()
        {
            var target = CircuitBreakerState.Closed;
            if (stateDescriptor?.State == CircuitBreakerState.Open)
            {
                target = CircuitBreakerState.HalfOpen;
            }
            var targetState = new CircuitBreakerStateDescriptor(target, Clock.Current.Now());
            await TransitionStateTo(targetState);
        }

        private async Task TransitionStateTo(CircuitBreakerStateDescriptor targetState, TimeSpan? expiry = null)
        {
            var serialsied = JsonConvert.SerializeObject(targetState, jsonSettings);
            var stateExpiry = targetState.State == CircuitBreakerState.Open && expiry == null
                ? TimeSpan.FromMinutes(1)
                : expiry;
            await Transistion(lastSerialisedState, serialsied, stateExpiry);
        }
        private async Task<string> Transistion(string fromState, string toState, TimeSpan? newStateExpiry = null)
        {
            var setCommand = newStateExpiry == null ? $"redis.call(\'set\',\'{key}\',\'{toState}\')" : $"redis.call(\'setex\',\'{key}\', {newStateExpiry.Value.TotalSeconds},\'{toState}\' )";
            var script = $"local prev = redis.call(\'get\', \'{key}\') if not(prev) or prev == \'{fromState}\' then {setCommand} return \'{toState}\' else return prev end";
            var execution = (await db.ScriptEvaluateAsync(script))?.ToString();
            return execution;
        }
        private async Task<CircuitBreakerStateDescriptor> ReadDescriptor()
        {
            var desc = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Now(),
                TimeSpan.FromMinutes(1));
            var src = await db.StringGetAsync(key);
            
            if (!src.IsNullOrEmpty)
            {
                desc = JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(src, jsonSettings);
            }

            return desc;
        }

        public void Dispose()
        {
            keySpaceSubscription?.Dispose();
            keySpaceSubscription = null;

            subscriber = null;

            keySapceObserver?.Dispose();
            connection?.Dispose();
            connection = null;
        }

        public IDisposable Subscribe(IObserver<CircuitBreakerStateDescriptor> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            observers.TryAdd(observer);
            return Disposable.Create(() => { observers.TryRemove(observer); });
        }

        void IObserver<(string key, string operation)>.OnCompleted()
        {
            keySpaceSubscription?.Dispose();
            keySpaceSubscription = null;
        }

        void IObserver<(string key, string operation)>.OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        void IObserver<(string key, string operation)>.OnNext((string key, string operation) value)
        {
            switch (value.operation)
            {
                case "expired":
                {
                    Transistion(
                        null,
                        JsonConvert.SerializeObject(new CircuitBreakerStateDescriptor(CircuitBreakerState.HalfOpen, Clock.Current.Now()), jsonSettings))
                        .Wait();
                }
                    break;
                default:
                    ReadDescriptor().ContinueWith(task =>
                    {
                        var desc = task.Result;
                        stateDescriptor = desc;
                        lastSerialisedState = JsonConvert.SerializeObject(stateDescriptor, jsonSettings);
                        foreach (var observer in observers) observer.OnNext(desc);
                    });
                    break;
            }
          
        }
    }
}