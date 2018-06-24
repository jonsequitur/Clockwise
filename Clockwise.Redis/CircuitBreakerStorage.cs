using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis
{
    public sealed class CircuitBreakerStorage : ICircuitBreakerStorage, IDisposable
    {
        private static readonly Logger Log = new Logger("CircuitBreakerStorage");
        private readonly RedisChannel channel;
        private ConnectionMultiplexer connection;
        private readonly IDatabase db;
        private readonly string key;
        private ISubscriber subscriber;
        private static readonly JsonSerializerSettings jsonSettings;
        private readonly ConcurrentSet <IObserver<CircuitBreakerStateDescriptor>> observers;

        static CircuitBreakerStorage()
        {
            jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            jsonSettings.Converters.Add(new StringEnumConverter());
            jsonSettings.NullValueHandling = NullValueHandling.Ignore;
        }

        public CircuitBreakerStorage(string connectionString, int dbId, Type commandType)
        {
            observers = new ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>>();
            connection = ConnectionMultiplexer.Connect(connectionString);
            db = connection.GetDatabase(dbId);

            key = $"{commandType.Name}.circuitBreaker";
            var topic = $"{dbId}.{key}";

            channel = new RedisChannel(topic, RedisChannel.PatternMode.Auto);
            subscriber = connection.GetSubscriber();

            subscriber.Subscribe(channel, OnStatusChange);
        }

        public async Task<CircuitBreakerStateDescriptor> GetStateAsync()
        {
            var stateDescriptor = await ReadDescriptor();
            return stateDescriptor;
        }

        public async Task SetStateAsync(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            var desc = new CircuitBreakerStateDescriptor(newState, Clock.Now(), expiry);
            var json = JsonConvert.SerializeObject(desc, jsonSettings);


            Log.Info("Setting circuitbreaker state to {state}", desc);
            await db.StringSetAsync(key, json, expiry);
            subscriber.Publish(channel, json, CommandFlags.HighPriority);
        }

        private async Task<CircuitBreakerStateDescriptor> ReadDescriptor()
        {
            var desc = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed);
            var src = await db.StringGetAsync(key);

            if (!src.IsNullOrEmpty)
            {
                desc = JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(src, jsonSettings);
            }

            return desc;
        }

        private void OnStatusChange(RedisChannel _, RedisValue value)
        {
            var newDescriptor = JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(value, jsonSettings);
            foreach (var observer in observers)
            {
                observer.OnNext(newDescriptor);
            }
        }

        public void Dispose()
        {
            subscriber?.Unsubscribe(channel);
            subscriber = null;
            connection?.Dispose();
            connection = null;
        }

        public IDisposable Subscribe(IObserver<CircuitBreakerStateDescriptor> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            observers.TryAdd(observer);
            return Disposable.Create(() => { observers.TryRemove(observer); });

        }
    }
}