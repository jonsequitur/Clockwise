using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class CircuitBreaker<T> : IDisposable
    where T : CircuitBreaker<T>
    {
        private readonly ICircuitBreakerBroker broker;

        private IDisposable storageSubscription;
        private CircuitBreakerStateDescriptor stateDescriptor;

        protected CircuitBreaker(ICircuitBreakerBroker broker)
        {
            this.broker = broker ?? throw new ArgumentNullException(nameof(broker));
            storageSubscription = this.broker.Subscribe<T>(CircuitBreakerStorageChanged);
        }

        private void CircuitBreakerStorageChanged(CircuitBreakerStateDescriptor descriptor) => stateDescriptor = descriptor;

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? broker.GetLastStateAsync<T>() : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess() => await broker.SignalSuccessAsync<T>();

        public async Task SignalFailure(TimeSpan expiry) => await broker.SignalFailureAsync<T>(expiry);

        public void Dispose()
        {
            storageSubscription?.Dispose();
            OnDispose();
        }

        protected virtual void OnDispose() {}
    }
}
