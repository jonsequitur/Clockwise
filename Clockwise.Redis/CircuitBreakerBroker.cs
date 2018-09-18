using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis
{
    public sealed class CircuitBreakerBroker : ICircuitBreakerBroker, IDisposable
    {
        private readonly int dbId;
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private readonly ConcurrentDictionary<string, Task<CircuitBreakerBrokerPartition>> partitions = new ConcurrentDictionary<string, Task<CircuitBreakerBrokerPartition>>();
        private readonly Lazy<Task<(ConnectionMultiplexer connection, IDatabase db, ISubscriber redisSubscriber)>> lazySetup;

        public CircuitBreakerBroker(string connectionString, int dbId = 0)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(connectionString));
            }

            this.dbId = dbId;

            lazySetup
                = new Lazy<Task<(ConnectionMultiplexer, IDatabase, ISubscriber)>>(async () =>
                {
                    //config set notify-keyspace-events KEs
                    var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
                    var subscriber = connection.GetSubscriber();
                    disposables.Add(() => subscriber.UnsubscribeAll());
                    disposables.Add(connection);
                    return (connection, connection.GetDatabase(dbId), subscriber);
                });
        }

        public async Task InitializeAsync(string circuitBreakerId)
        {
            var setup = await lazySetup.Value;
            var db = setup.db;
            var redisSubscriber = setup.redisSubscriber;

            var keySpace = circuitBreakerId;
            var partition = await partitions.GetOrAdd(keySpace,
                                                      async redisKey => new CircuitBreakerBrokerPartition(redisKey, dbId, db));

            await partition.Initialize(redisSubscriber);
        }

        public async Task<CircuitBreakerStateDescriptor> GetLastStateAsync(string circuitBreakerId)
        {
            var partition =await GetPartitionAsync(circuitBreakerId);
          return  await partition.GetLastStateAsync();
        }

        public async Task SignalFailureAsync(string circuitBreakerId, TimeSpan expiry)
        {
            var partition = await GetPartitionAsync(circuitBreakerId);
            await partition.SignalFailureAsync(expiry);
        }

        private async Task<CircuitBreakerBrokerPartition> GetPartitionAsync(string circuitBreakerId)
        {
            var keySpace = circuitBreakerId;
            var partition = await partitions.GetOrAdd(keySpace, async redisKey =>
            {
                var setup = await lazySetup.Value;
                var db = setup.db;
                var keyPartition = new CircuitBreakerBrokerPartition(redisKey, dbId, db);
                return keyPartition;
            });
            return partition;
        }

        public async Task SignalSuccessAsync(string circuitBreakerId)
        {
            var partition = await GetPartitionAsync(circuitBreakerId);

            await partition.SignalSuccessAsync();
        }

        public async Task SubscribeAsync(string circuitBreakerId, CircuitBreakerBrokerSubscriber subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            var partition = await GetPartitionAsync(circuitBreakerId);

            disposables.Add(partition.Subscribe(subscriber));
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
