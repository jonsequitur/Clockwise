using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Clockwise.Redis
{
    public sealed class CircuitBreakerStorage : ICircuitBreakerStorage, IDisposable
    {
        private readonly RedisCircuitBreakerStorageSettings settings;
        private ConnectionMultiplexer connection;
        private IDatabase db;
        private ISubscriber subscriber;
        private readonly ConcurrentDictionary<string, CircuitBreakerStoragePartition> partitions = new ConcurrentDictionary<string, CircuitBreakerStoragePartition>();

        public CircuitBreakerStorage(RedisCircuitBreakerStorageSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task InitializeFor<T>() where T : CircuitBreaker<T>
        {
            if (connection == null)
            {
                connection = await ConnectionMultiplexer.ConnectAsync(settings.ConnectionString);
                db = connection.GetDatabase(settings.DbId);
                subscriber = connection.GetSubscriber();
            }

            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, settings.DbId, db);
                return keyPartition;
            });

            await partition.Initialize(subscriber);
        }

        private string GetKey<T>()
        {
            return $"{typeof(T).Name}.circuitBreaker";
        }
  
        public Task<CircuitBreakerStateDescriptor> GetLastStateOfAsync<T>() where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, settings.DbId, db);
                return keyPartition;
            });

            return partition.GetLastStateAsync();
        }
        public  Task SignalFailureForAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, settings.DbId, db);
                return keyPartition;
            });

            return partition.SignalFailureAsync(expiry);
        }

        public Task SignalSuccessForAsync<T>() where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, settings.DbId, db);
                return keyPartition;
            });

            return partition.SignalSuccessAsync();
        }

        public void Dispose()
        {
            subscriber = null;
            connection?.Dispose();
            connection = null;
        }

        public IDisposable Subscribe<T>(IObserver<CircuitBreakerStateDescriptor> observer) where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerStoragePartition(redisKey, settings.DbId, db);
                return keyPartition;
            });

            return partition.Subscribe(observer);
        }
    }
}