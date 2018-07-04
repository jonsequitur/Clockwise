using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public sealed class InMemoryCircuitBreakerBroker : ICircuitBreakerBroker
    {

        private readonly ConcurrentDictionary<Type, CircuitBreakerStoragePartition> partitions = new ConcurrentDictionary<Type, CircuitBreakerStoragePartition>();
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

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync<T>() where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition(typeof(T).Name));
            return partition.GetLastStateAsync();
        }

        public Task SignalFailureAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition(typeof(T).Name));
            return partition.SignalFailureAsync(expiry);
        }

        public Task SignalSuccessAsync<T>() where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition(typeof(T).Name));
            return partition.SignalSuccessAsync();
        }

        public void Subscribe<T>(CircuitBreakerBrokerSubscriber subscriber) where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition(typeof(T).Name));
            partition.Subscribe(subscriber);
        }

        public Task InitializeFor<T>() where T : CircuitBreaker<T>
        {
            return Task.CompletedTask;
        }
    }
}