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
            private readonly ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>> observers;
            private CircuitBreakerStateDescriptor stateDescriptor;

            public CircuitBreakerStoragePartition()
            {
                observers = new ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>>();
                stateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now(), TimeSpan.FromMinutes(2));
            }

            public IDisposable Subscribe(IObserver<CircuitBreakerStateDescriptor> observer)
            {
                if (observer == null) throw new ArgumentNullException(nameof(observer));
                observer.OnNext(stateDescriptor);
                observers.TryAdd(observer);
                return Disposable.Create(() => { observers.TryRemove(observer); });
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
               
                var open = new CircuitBreakerStateDescriptor(CircuitBreakerState.Open, Clock.Current.Now(), expiry);
                Clock.Current.Schedule(_=>
                {
                    if (ReferenceEquals(stateDescriptor, open))
                    {
                        stateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.HalfOpen, Clock.Current.Now());
                        NotifyState();
                    }
                },expiry);
                stateDescriptor = open;
                NotifyState();
                await Task.Yield();
            }

            private void NotifyState()
            {
                foreach (var observer in observers)
                {
                   observer.OnNext(stateDescriptor);
                }
            }

            public async Task SignalSuccessAsync()
            {
                if (stateDescriptor?.State != CircuitBreakerState.Closed)
                {
                    stateDescriptor = new CircuitBreakerStateDescriptor(stateDescriptor?.State == CircuitBreakerState.Open ? CircuitBreakerState.HalfOpen: CircuitBreakerState.Closed, Clock.Current.Now());
                }
                NotifyState();
                await Task.Yield();
            }
        }

        public Task<CircuitBreakerStateDescriptor> GetLastStateOfAsync<T>() where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition());
            return partition.GetLastStateAsync();
        }

        public Task SignalFailureForAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition());
            return partition.SignalFailureAsync(expiry);
        }

        public Task SignalSuccessForAsync<T>() where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition());
            return partition.SignalSuccessAsync();
        }

        public IDisposable Subscribe<T>(IObserver<CircuitBreakerStateDescriptor> observer) where T : CircuitBreaker<T>
        {
            var partition = partitions.GetOrAdd(typeof(T), key => new CircuitBreakerStoragePartition());

            return partition.Subscribe(observer);
        }


    }
}