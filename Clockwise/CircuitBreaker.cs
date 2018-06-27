using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class CircuitBreaker<T> : IDisposable
    where T : CircuitBreaker<T>
    {
        private readonly ICircuitBreakerStorage storage;
        private IDisposable setStateSubscription;
        private IDisposable storageSubscription;
        private CircuitBreakerStateDescriptor stateDescriptor;

        protected CircuitBreaker(ICircuitBreakerStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            storageSubscription = this.storage.Subscribe<T>(Subscriber);
        }

        private void Subscriber(CircuitBreakerStateDescriptor circuitbreakerstatedescriptor) => stateDescriptor = circuitbreakerstatedescriptor;

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? storage.GetLastStateAsync<T>() : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess() => await storage.SignalSuccessAsync<T>();

        public async Task SignalFailure(TimeSpan expiry) => await storage.SignalFailureAsync<T>(expiry);

        public void Dispose()
        {
            setStateSubscription?.Dispose();
            setStateSubscription = null;
            storageSubscription?.Dispose();
            storageSubscription = null;
            OnDispose();
        }

        protected virtual void OnDispose() {}
    }
}
