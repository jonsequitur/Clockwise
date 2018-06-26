using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class CircuitBreaker<T> : IObserver<CircuitBreakerStateDescriptor>
    where T : CircuitBreaker<T>
    {
        private readonly ICircuitBreakerStorage storage;
        private IDisposable setStateSubscription;
        private IDisposable storageSubscription;
        private CircuitBreakerStateDescriptor stateDescriptor;

        protected CircuitBreaker(ICircuitBreakerStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            storageSubscription = this.storage.Subscribe<T>(this);
        }
        
        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? storage.GetLastStateOfAsync<T>() : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess()
        {
            await storage.SignalSuccessForAsync<T>();
        }

        public async Task SignalFailure(TimeSpan expiry)
        {
            await storage.SignalFailureForAsync<T>(expiry);
        }

        public void Dispose()
        {
            setStateSubscription?.Dispose();
            setStateSubscription = null;
            storageSubscription?.Dispose();
            storageSubscription = null;

        }

        void IObserver<CircuitBreakerStateDescriptor>.OnCompleted()
        {
        }

        void IObserver<CircuitBreakerStateDescriptor>.OnError(Exception error)
        {
        }

        void IObserver<CircuitBreakerStateDescriptor>.OnNext(CircuitBreakerStateDescriptor value)
        {
            stateDescriptor = value;
        }
    }
}
