using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public sealed class InMemoryCircuitBreakerStorage : ICircuitBreakerStorage
    {

        private readonly ConcurrentDictionary<Type, CircuitBreakerStoragePartition> partitions = new ConcurrentDictionary<Type, CircuitBreakerStoragePartition>();
        private class CircuitBreakerStoragePartition
        {
            private readonly ConcurrentSet<CircuitBreakerStateDescriptorSubscriber> subscribers;
            private CircuitBreakerStateDescriptor stateDescriptor;

            public CircuitBreakerStoragePartition()
            {
                subscribers = new ConcurrentSet<CircuitBreakerStateDescriptorSubscriber>();
                stateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now(), TimeSpan.FromMinutes(2));
            }

            public IDisposable Subscribe(CircuitBreakerStateDescriptorSubscriber subscriber)
            {
                if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
                subscriber(stateDescriptor);
                subscribers.TryAdd(subscriber);
                return Disposable.Create(() => { subscribers.TryRemove(subscriber); });
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
                        stateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.HalfOpen, Clock.Current.Now());
                        NotifyState();
                    }
                }, expiry);
                stateDescriptor = open;
                NotifyState();
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
                    stateDescriptor = new CircuitBreakerStateDescriptor(stateDescriptor?.State == CircuitBreakerState.Open ? CircuitBreakerState.HalfOpen : CircuitBreakerState.Closed, Clock.Current.Now());
                }
                NotifyState();
            }
        }

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync<T>() where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition());
            return partition.GetLastStateAsync();
        }

        public Task SignalFailureAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition());
            return partition.SignalFailureAsync(expiry);
        }

        public Task SignalSuccessAsync<T>() where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition());
            return partition.SignalSuccessAsync();
        }

        public IDisposable Subscribe<T>(CircuitBreakerStateDescriptorSubscriber subscriber) where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition());
            return partition.Subscribe(subscriber);
        }


    }
}