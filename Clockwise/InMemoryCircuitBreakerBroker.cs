using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public sealed class InMemoryCircuitBreakerBroker : ICircuitBreakerBroker
    {

        private readonly ConcurrentDictionary<string, CircuitBreakerStoragePartition> partitions = new ConcurrentDictionary<string, CircuitBreakerStoragePartition>();
        private class CircuitBreakerStoragePartition
        {
            private static readonly Logger logger = Logger<CircuitBreakerStoragePartition>.Log;
            private readonly ConcurrentSet<CircuitBreakerBrokerSubscriber> subscribers;
            private CircuitBreakerStateDescriptor stateDescriptor;
            private readonly string key;

            public CircuitBreakerStoragePartition(string key)
            {
                this.key = key;
                subscribers = new ConcurrentSet<CircuitBreakerBrokerSubscriber>();
                stateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now(), TimeSpan.FromMinutes(2));
            }

            public void Subscribe(CircuitBreakerBrokerSubscriber subscriber)
            {
                if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
                subscriber(stateDescriptor);
                subscribers.TryAdd(subscriber);
            }

            public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
            {
                if (stateDescriptor == null)
                {
                    stateDescriptor =
                        new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now());
                }

                return Task.FromResult(stateDescriptor);
            }

            public async Task SignalFailureAsync(TimeSpan expiry)
            {
                await Task.Yield();
                var open = new CircuitBreakerStateDescriptor(CircuitBreakerState.Open, Clock.Current.Now(), expiry);
                Clock.Current.Schedule(_ =>
                {
                    if (ReferenceEquals(stateDescriptor, open))
                    {
                        var halfOpen = new CircuitBreakerStateDescriptor(CircuitBreakerState.HalfOpen, Clock.Current.Now());
                        SetCurrentState(halfOpen);
                        NotifyState();
                    }
                }, expiry);
                SetCurrentState(open);
                NotifyState();
            }

            private void SetCurrentState(CircuitBreakerStateDescriptor newState)
            {
                if (stateDescriptor != newState)
                {
                    stateDescriptor = newState;
                    logger.Event("CircuitBreakerTransition", ("circuitBreakerType", key),
                        ("circuitBreakerState", newState));
                }
            }

            private void NotifyState()
            {
                foreach (var subscriber in subscribers)
                {
                    subscriber(stateDescriptor);
                }
            }

            public async Task SignalSuccessAsync()
            {
                await Task.Yield();
                if (stateDescriptor?.State != CircuitBreakerState.Closed)
                {
                    var newState = new CircuitBreakerStateDescriptor(stateDescriptor?.State == CircuitBreakerState.Open ? CircuitBreakerState.HalfOpen : CircuitBreakerState.Closed, Clock.Current.Now());
                    SetCurrentState(newState);
                }
                NotifyState();
            }
        }

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync(string circuitBreakerId)
        {
            var partition = partitions.GetOrAdd(circuitBreakerId, key => new CircuitBreakerStoragePartition(circuitBreakerId));
            return partition.GetLastStateAsync();
        }

        public Task SignalFailureAsync(string circuitBreakerId, TimeSpan expiry)
        {
            var partition = partitions.GetOrAdd(circuitBreakerId, key => new CircuitBreakerStoragePartition(circuitBreakerId));
            return partition.SignalFailureAsync(expiry);
        }

        public Task SignalSuccessAsync(string circuitBreakerId)
        {
            var partition = partitions.GetOrAdd(circuitBreakerId, key => new CircuitBreakerStoragePartition(circuitBreakerId));
            return partition.SignalSuccessAsync();
        }

        public void Subscribe(string circuitBreakerId, CircuitBreakerBrokerSubscriber subscriber)
        {
            var partition = partitions.GetOrAdd(circuitBreakerId, key => new CircuitBreakerStoragePartition(circuitBreakerId));
            partition.Subscribe(subscriber);
        }

        public Task InitializeFor(string circuitBreakerId)
        {
            return Task.CompletedTask;
        }
    }
}