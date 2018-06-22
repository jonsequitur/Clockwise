using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public sealed class InMemoryCircuitBreakerStorage : ICircuitBreakerStorage
    {
        private ImmutableList<IObserver<CircuitBreakerStateDescriptor>> observers;
        private CircuitBreakerStateDescriptor stateDescriptor;

        public InMemoryCircuitBreakerStorage()
        {
            observers = ImmutableList<IObserver<CircuitBreakerStateDescriptor>>.Empty;
            stateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now());
        }

        public void Dispose()
        {
        }

        public Task<CircuitBreakerStateDescriptor> GetStateAsync()
        {
            return Task.FromResult(stateDescriptor);
        }

        public async Task SetStateAsync(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            var desc = new CircuitBreakerStateDescriptor(newState, Clock.Current.Now(), expiry);
            await Task.Yield();
            if (desc != stateDescriptor)
            {
                stateDescriptor = desc;
                foreach (var observer in observers) observer.OnNext(stateDescriptor);
            }
        }

        public IDisposable Subscribe(IObserver<CircuitBreakerStateDescriptor> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            observer.OnNext(stateDescriptor);
            observers = observers.Add(observer);
            return Disposable.Create(() => { observers = observers.Remove(observer); });
        }
    }
}