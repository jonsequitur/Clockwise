using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Clockwise.Redis
{
    public sealed class CircuitBreakerStorage : ICircuitBreakerStorage, IDisposable
    {
        private readonly string connectionString;
        private readonly int dbId;

        private IDatabase db;
        private ISubscriber subscriber;
        private readonly ConcurrentDictionary<string, CircuitBreakerStoragePartition> partitions = new ConcurrentDictionary<string, CircuitBreakerStoragePartition>();

        public CircuitBreakerStorage(string connectionString, int dbId)
        {
            this.connectionString = connectionString;
            this.dbId = dbId;

            LazyConnection
                = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(connectionString));
        }

        private Lazy<ConnectionMultiplexer> LazyConnection { get; set; }

        public async Task InitializeFor<T>() where T : CircuitBreaker<T>
        {
            var connection = LazyConnection.Value;
            db = connection.GetDatabase(dbId);
            subscriber = connection.GetSubscriber();


            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, dbId, db);
                return keyPartition;
            });

            await partition.Initialize(subscriber);
        }

        private static string GetKey<T>()
        {
            return $"{typeof(T).Name}.circuitBreaker";
        }

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync<T>() where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, dbId, db);
                return keyPartition;
            });

            return partition.GetLastStateAsync();
        }
        public Task SignalFailureAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, dbId, db);
                return keyPartition;
            });

            return partition.SignalFailureAsync(expiry);
        }

        public Task SignalSuccessAsync<T>() where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, dbId, db);
                return keyPartition;
            });

            return partition.SignalSuccessAsync();
        }

        public void Dispose()
        {
            subscriber = null;
            if (LazyConnection.IsValueCreated)
            {
                var connection = LazyConnection.Value;
                LazyConnection = null;
                connection?.Dispose();
            }
        }

        public IDisposable Subscribe<T>(CircuitBreakerStorageSubscriber subscriber) where T : CircuitBreaker<T>
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, dbId, db);
                return keyPartition;
            });

            return partition.Subscribe(subscriber);
        }
    }
}