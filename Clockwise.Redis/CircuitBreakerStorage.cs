using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis
{
    public sealed class CircuitBreakerStorage : ICircuitBreakerStorage
    {
        private static readonly Logger Log = new Logger("CircuitBreakerStorage");
        private readonly RedisChannel channel;
        private readonly ConnectionMultiplexer connection;
        private readonly IDatabase db;
        private readonly string key;
        private readonly ISubscriber subscriber;
        private static readonly JsonSerializerSettings jsonSettings;

        static CircuitBreakerStorage()
        {
            jsonSettings = new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()};
            jsonSettings.Converters.Add(new StringEnumConverter());
            jsonSettings.NullValueHandling = NullValueHandling.Ignore;
        }

        public event EventHandler<CircuitBreakerStateDescriptor> CircuitBreakerStateChanged; 
        public static CircuitBreakerStorage Create<T>(string connectionString, int dbId = -1)
        {
            return new CircuitBreakerStorage(connectionString, dbId, typeof(T));
        }

        private CircuitBreakerStorage(string connectionString, int dbId, Type commandType)
        {
            connection = ConnectionMultiplexer.Connect(connectionString);
            db = connection.GetDatabase(dbId);

            key = $"{commandType.Name}.circuitBreaker";
            var topic = $"{dbId}.{key}";

            channel = new RedisChannel(topic, RedisChannel.PatternMode.Auto);
            subscriber = connection.GetSubscriber();

            StateDescriptor = ReadDescriptor();
            subscriber.Subscribe(channel, OnStatusChange);
        }

        public CircuitBreakerStateDescriptor StateDescriptor { get; private set; }

        public void SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            var desc = new CircuitBreakerStateDescriptor(newState, Clock.Now(), expiry);
            var json = JsonConvert.SerializeObject(desc, jsonSettings);


            Log.Info("Setting circuitbreaker state to {state}", desc);
            db.StringSet(key, json, expiry);
            subscriber.Publish(channel, json, CommandFlags.HighPriority);
        }

        private CircuitBreakerStateDescriptor ReadDescriptor()
        {
            var desc = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed);
            var src = db.StringGet(key);

            if (!src.IsNullOrEmpty) desc = JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(src, jsonSettings);

            return desc;
        }

        private void OnStatusChange(RedisChannel _, RedisValue value)
        {
            var newDescriptor = JsonConvert.DeserializeObject<CircuitBreakerStateDescriptor>(value, jsonSettings);

            if (newDescriptor != StateDescriptor)
            {
                Log.Info("Received circuitbreaker state update to {state}", newDescriptor);

                StateDescriptor = newDescriptor;
                CircuitBreakerStateChanged ?.Invoke(this, StateDescriptor);
            }
        }
        public void Dispose()
        {
            subscriber.Unsubscribe(channel);
            connection.Dispose();
        }
    }
}