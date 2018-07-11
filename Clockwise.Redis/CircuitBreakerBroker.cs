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

        public async Task InitializeFor(string circuitBreakerId)
        {
            var setup = lazySetup.Value;
            var db = setup.db;
            var redisSubscriber = setup.redisSubscriber;

            var keySpace = circuitBreakerId;
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var keyPartition = new CircuitBreakerBrokerPartition(redisKey, dbId, db);
                return keyPartition;
            });

            await partition.Initialize(redisSubscriber);
        }

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync(string circuitBreakerId)
        {
            var partition = GetPartition(circuitBreakerId);
            return partition.GetLastStateAsync();
        }
        public Task SignalFailureAsync(string circuitBreakerId, TimeSpan expiry)
        {
            var partition = GetPartition(circuitBreakerId);
            return partition.SignalFailureAsync(expiry);
        }

        private CircuitBreakerBrokerPartition GetPartition(string circuitBreakerId)
        {
            var keySpace = circuitBreakerId;
            var partition = partitions.GetOrAdd(keySpace, redisKey =>
            {
                var setup = lazySetup.Value;
                var db = setup.db;
                var keyPartition = new CircuitBreakerBrokerPartition(redisKey, dbId, db);
                return keyPartition;
            });
            return partition;
        }

        public Task SignalSuccessAsync(string circuitBreakerId)
        {
            var partition = GetPartition(circuitBreakerId);
            return partition.SignalSuccessAsync();
        }

        public void Subscribe(string circuitBreakerId, CircuitBreakerBrokerSubscriber subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            var partition = GetPartition(circuitBreakerId);
            disposables.Add(partition.Subscribe(subscriber));
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}