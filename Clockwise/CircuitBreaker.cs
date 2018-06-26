using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class CircuitBreaker:  IDisposable, IObserver<CircuitBreakerStateDescriptor>
    {
        private readonly ICircuitBreakerStorage storage;
        private IDisposable setStateSubscription;
        private IDisposable storageSubscription;

        /// <summary>
        /// Initializes a new instance of the <see /> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        /// <exception cref="ArgumentNullException">storage</exception>
        protected CircuitBreaker(ICircuitBreakerStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            storageSubscription = this.storage.Subscribe(this);
        }

        private CircuitBreakerStateDescriptor stateDescriptor;

     
       
        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? storage.GetLastStateAsync() : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess()
        {
            await storage.SignalSuccessAsync();
        }

        public async Task SignalFailure(TimeSpan expiry)
        {
            await storage.SignalFailureAsync(expiry);
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
