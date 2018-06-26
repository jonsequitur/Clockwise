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
    internal class CircuitBreakerStoragePartition : IObserver<(string key, string operation)>, IObservable<CircuitBreakerStateDescriptor>, IDisposable
    {
        private static readonly JsonSerializerSettings JsonSerializationSettings;
        private readonly ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>> observers;
        private CircuitBreakerStateDescriptor stateDescriptor;
        private string lastSerialisedState;
        private readonly string key;
        private readonly int dbId;
        private readonly IDatabase db;
        private KeySpaceObserver keySpaceObserver;
        private IDisposable keySpaceSubscription;

        static CircuitBreakerStoragePartition()
        {
            JsonSerializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            JsonSerializationSettings.Converters.Add(new StringEnumConverter());
            JsonSerializationSettings.NullValueHandling = NullValueHandling.Ignore;
        }

        public CircuitBreakerStoragePartition(string key, int dbId, IDatabase db)
        {
            if (IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(key));
            }
            this.key = key;
            this.dbId = dbId;
            this.db = db;
            observers = new ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>>();
        }

        public async Task SignalFailureAsync(TimeSpan expiry)
        {
            var openState = new CircuitBreakerStateDescriptor(CircuitBreakerState.Open, Clock.Current.Now(), expiry);
            await TransitionStateTo(openState, expiry);
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

        public async Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            var serialised = await db.StringGetAsync(key);
            lastSerialisedState = serialised.HasValue ? serialised.ToString() : Empty;
            if (IsNullOrWhiteSpace(serialised))
            {
                var newState = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now(),
                    TimeSpan.FromMinutes(1));
                lastSerialisedState = await Transistion(lastSerialisedState,
                    JsonConvert.SerializeObject(newState, JsonSerializationSettings));
            }

            return TryDeserialise(lastSerialisedState);
        }

        private static CircuitBreakerStateDescriptor TryDeserialise(string serialised)
        {
            return IsNullOrWhiteSpace(serialised)
                ? null
                : JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(serialised, JsonSerializationSettings);
        }
        private async Task TransitionStateTo(CircuitBreakerStateDescriptor targetState, TimeSpan? expiry = null)
        {
            var serialsied = JsonConvert.SerializeObject(targetState, JsonSerializationSettings);
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
                desc = JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(src, JsonSerializationSettings);
            }

            return desc;
        }

        public async Task Initialize(ISubscriber subscriber)
        {
   
            keySpaceObserver = new KeySpaceObserver(dbId, key, subscriber);
            keySpaceSubscription = keySpaceObserver.Subscribe(this);
            await keySpaceObserver.Initialize();
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
                            JsonConvert.SerializeObject(new CircuitBreakerStateDescriptor(CircuitBreakerState.HalfOpen, Clock.Current.Now()), JsonSerializationSettings))
                        .Wait();
                }
                    break;
                default:
                    ReadDescriptor().ContinueWith(task =>
                    {
                        var desc = task.Result;
                        stateDescriptor = desc;
                        lastSerialisedState = JsonConvert.SerializeObject(stateDescriptor, JsonSerializationSettings);
                        foreach (var observer in observers) observer.OnNext(desc);
                    });
                    break;
            }

        }
        public void Dispose()
        {
            keySpaceSubscription?.Dispose();
            keySpaceSubscription = null;

            keySpaceObserver?.Dispose();
            keySpaceObserver = null;
        }

        public IDisposable Subscribe(IObserver<CircuitBreakerStateDescriptor> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            observers.TryAdd(observer);
            return Disposable.Create(() => observers.TryRemove(observer));
        }
    }
}