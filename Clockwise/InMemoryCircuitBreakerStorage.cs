using System;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public sealed class InMemoryCircuitBreakerStorage : ICircuitBreakerStorage
    {
        private readonly ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>> observers;
        private CircuitBreakerStateDescriptor stateDescriptor;

        public InMemoryCircuitBreakerStorage()
        {
            observers = new ConcurrentSet<IObserver<CircuitBreakerStateDescriptor>>();
            stateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now(), TimeSpan.FromMinutes(2));
        }

        public Task<CircuitBreakerStateDescriptor> GetStateAsync()
        {
            return Task.FromResult(stateDescriptor);
        }

        public async Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            await Task.Yield();
            return stateDescriptor;
        }

        public async Task SetStateAsync(CircuitBreakerState newState, TimeSpan expiry)
        {
            var desc = new CircuitBreakerStateDescriptor(newState, Clock.Current.Now(), expiry);
            if (desc != stateDescriptor)
            {
                stateDescriptor = desc;
                foreach (var observer in observers) observer.OnNext(stateDescriptor);
            }

            
            await Task.Yield();
        }

        public IDisposable Subscribe(IObserver<CircuitBreakerStateDescriptor> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            observer.OnNext(stateDescriptor);
            observers.TryAdd(observer);
            return Disposable.Create(() => { observers.TryRemove(observer); });
        }
    }
}