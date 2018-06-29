using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis
{
    public sealed class CircuitBreakerBroker : ICircuitBreakerBroker, IDisposable
    {
        private readonly int dbId;
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private readonly ConcurrentDictionary<string, CircuitBreakerBrokerPartition> partitions = new ConcurrentDictionary<string, CircuitBreakerBrokerPartition>();
        private readonly Lazy<(ConnectionMultiplexer connection, IDatabase db, ISubscriber redisSubscriber)> lazySetup;
        

        public CircuitBreakerBroker(string connectionString, int dbId)
        {
            this.dbId = dbId;
            lazySetup
                = new Lazy<(ConnectionMultiplexer, IDatabase, ISubscriber)>(() =>
                {
                    var connection = ConnectionMultiplexer.Connect(connectionString);
                    var subscriber = connection.GetSubscriber();
                    disposables.Add(Disposable.Create(() => subscriber.UnsubscribeAll()));
                    disposables.Add(connection);
                    return (connection, connection.GetDatabase(dbId), subscriber);
                });
        }

        public async Task InitializeFor<T>() where T : CircuitBreaker<T>
        {
            var setup = lazySetup.Value;
            var db = setup.db;
            var redisSubscriber = setup.redisSubscriber;

            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerBrokerPartition(redisKey, dbId, db);
                return keyPartition;
            });

            await partition.Initialize(redisSubscriber);
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
                var setup = lazySetup.Value;
                var db = setup.db;
                var keyPartition = new CircuitBreakerBrokerPartition(redisKey, dbId, db);
                return keyPartition;
            });

            return partition.GetLastStateAsync();
        }
        public Task SignalFailureAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var setup = lazySetup.Value;
                var db = setup.db;
                var keyPartition = new CircuitBreakerBrokerPartition(redisKey, dbId, db);
                return keyPartition;
            });

            return partition.SignalFailureAsync(expiry);
        }

        public Task SignalSuccessAsync<T>() where T : CircuitBreaker<T>
        {
            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var setup = lazySetup.Value;
                var db = setup.db;
                var keyPartition = new CircuitBreakerBrokerPartition(redisKey, dbId, db);
                return keyPartition;
            });

            return partition.SignalSuccessAsync();
        }

        public IDisposable Subscribe<T>(CircuitBreakerBrokerSubscriber subscriber) where T : CircuitBreaker<T>
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            var keySpace = GetKey<T>();
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var setup = lazySetup.Value;
                var db = setup.db;
                var keyPartition = new CircuitBreakerBrokerPartition(redisKey, dbId, db);
                return keyPartition;
            });

            return partition.Subscribe(subscriber);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}