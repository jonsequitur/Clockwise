using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class CircuitBreaker<T> : IDisposable
    where T : CircuitBreaker<T>
    {
        private readonly ICircuitBreakerStorage storage;

        private IDisposable storageSubscription;
        private CircuitBreakerStateDescriptor stateDescriptor;

        protected CircuitBreaker(ICircuitBreakerStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            storageSubscription = this.storage.Subscribe<T>(CircuitBreakerStorageChanged);
        }

        private void CircuitBreakerStorageChanged(CircuitBreakerStateDescriptor descriptor) => stateDescriptor = descriptor;

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? storage.GetLastStateAsync<T>() : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess() => await storage.SignalSuccessAsync<T>();

        public async Task SignalFailure(TimeSpan expiry) => await storage.SignalFailureAsync<T>(expiry);

        public void Dispose()
        {
            storageSubscription?.Dispose();
            OnDispose();
        }

        protected virtual void OnDispose() {}
    }
}
