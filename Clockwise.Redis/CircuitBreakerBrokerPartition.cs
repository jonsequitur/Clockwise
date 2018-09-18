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
    internal class CircuitBreakerBrokerPartition : IDisposable
    {
        private static readonly Logger logger = Logger<CircuitBreakerBrokerPartition>.Log;
        private static readonly JsonSerializerSettings JsonSerializationSettings;
        private readonly ConcurrentSet<CircuitBreakerBrokerSubscriber> subscribers;
        private CircuitBreakerStateDescriptor stateDescriptor;
        private string lastSerializedState;
        private readonly string key;
        private readonly int dbId;
        private readonly IDatabase db;
        private KeySpaceObserver keySpaceObserver;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        static CircuitBreakerBrokerPartition()
        {
            JsonSerializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            JsonSerializationSettings.Converters.Add(new StringEnumConverter());
            JsonSerializationSettings.NullValueHandling = NullValueHandling.Ignore;
        }

        public CircuitBreakerBrokerPartition(string key, int dbId, IDatabase db)
        {
            if (IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(key));
            }
            this.key = key;
            this.dbId = dbId;
            this.db = db;
            subscribers = new ConcurrentSet<CircuitBreakerBrokerSubscriber>();
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
            lastSerializedState = serialised.HasValue ? serialised.ToString() : Empty;

            if (IsNullOrWhiteSpace(serialised))
            {
                var newState = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now(),
                    TimeSpan.FromMinutes(1));
                lastSerializedState = await Transition(lastSerializedState,
                    JsonConvert.SerializeObject(newState, JsonSerializationSettings),true);
            }

            return TryDeserialise(lastSerializedState);
        }

        private static CircuitBreakerStateDescriptor TryDeserialise(string serialised)
        {
            return IsNullOrWhiteSpace(serialised)
                ? null
                : JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(serialised, JsonSerializationSettings);
        }

        private async Task TransitionStateTo(CircuitBreakerStateDescriptor targetState, TimeSpan? expiry = null)
        {
            var shoudLogEvent = IsNullOrWhiteSpace(lastSerializedState) || targetState.State == CircuitBreakerState.Open || stateDescriptor?.State != targetState.State;
            var serialized = JsonConvert.SerializeObject(targetState, JsonSerializationSettings);
            var stateExpiry = targetState.State == CircuitBreakerState.Open && expiry == null
                ? TimeSpan.FromMinutes(1)
                : expiry;
            await Transition(lastSerializedState, serialized, shoudLogEvent, stateExpiry);
        }

        private async Task<string> Transition(string fromState, string toState, bool shouldLogEvent, TimeSpan? newStateExpiry = null)
        {
            string setCommand;
            if (newStateExpiry == null)
            {
                setCommand = $"redis.call(\'set\',\'{key}\',\'{toState}\')";
            }
            else
            {
                setCommand = $"redis.call(\'setex\',\'{key}\', {newStateExpiry.Value.TotalSeconds},\'{toState}\' )";
            }

            var script = $"local prev = redis.call(\'get\', \'{key}\') if not(prev) or prev == \'{fromState}\' then {setCommand} return \'{toState}\' else return prev end";
            var execution = (await db.ScriptEvaluateAsync(script))?.ToString();
            if (shouldLogEvent && !IsNullOrWhiteSpace(execution) && execution == toState)
            {
                logger.Event("CircuitBreakerTransition",("circuitBreakerType",key),("circuitBreakerState", execution ));
            }
            return execution;
        }

        private async Task<CircuitBreakerStateDescriptor> ReadDescriptor()
        {
            var desc = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Now());
            var src = await db.StringGetAsync(key);

            if (!src.IsNullOrEmpty && !IsNullOrWhiteSpace(src))
            {
                desc = JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(src, JsonSerializationSettings);
            }

            return desc;
        }

        public async Task Initialize(ISubscriber redisSubscriber)
        {
            if (keySpaceObserver == null)
            {
                keySpaceObserver = new KeySpaceObserver(dbId, key, redisSubscriber);
                var keySpaceSubscription = keySpaceObserver.Subscribe(KeySpaceNotificationHandler);
                disposables.Add(keySpaceObserver);
                disposables.Add(keySpaceSubscription);
                await keySpaceObserver.Initialize();
            }
        }

        private void KeySpaceNotificationHandler(string target, string operation)
        {
            if (key != target)
            {
                return;

            }
            switch (operation)
            {
                case "expired":
                {
                       Task.Run(() =>Transition(
                            null,
                            JsonConvert.SerializeObject(
                                new CircuitBreakerStateDescriptor(CircuitBreakerState.HalfOpen, Clock.Current.Now()),
                                JsonSerializationSettings),true));

                }
                    break;
                default:
                    ReadDescriptor().ContinueWith(task =>
                    {
                        var desc = task.Result;
                        stateDescriptor = desc;
                        lastSerializedState = JsonConvert.SerializeObject(stateDescriptor, JsonSerializationSettings);
                        foreach (var observer in subscribers) observer(desc);
                    });
                    break;
            }
        }

        public void Dispose()
        {
           disposables.Dispose();
        }

        public IDisposable Subscribe(CircuitBreakerBrokerSubscriber subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }
            subscribers.TryAdd(subscriber);
            return Disposable.Create(() => subscribers.TryRemove(subscriber));
        }
    }
}