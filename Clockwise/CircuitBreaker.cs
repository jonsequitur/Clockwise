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

        private async Task SetState(CircuitBreakerState newState, TimeSpan expiry)
        {
            await storage.SetStateAsync(newState, expiry);
        }

        public async Task Open(TimeSpan expiry)
        {
            if (expiry.TotalSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expiry));
            }

            await SetState(CircuitBreakerState.Open, expiry);
            
        }

        public async Task HalfOpen()
        {
            if (stateDescriptor.State == CircuitBreakerState.Open)
            {
                await SetState(CircuitBreakerState.HalfOpen, TimeSpan.FromMinutes(1));
            }
            else
            {
                throw new InvalidOperationException("Can transition to half open only from open state");
            }
        }

        public async Task Close()
        {
            if (stateDescriptor.State != CircuitBreakerState.Closed)
            {
                await SetState(CircuitBreakerState.Closed, TimeSpan.FromMinutes(1));
            }
        }

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return storage.GetLastStateAsync();
        }

        public async Task SignalSuccess()
        {
            switch (stateDescriptor.State)
            {
                case CircuitBreakerState.Open:
                    await HalfOpen();
                    break;
                case CircuitBreakerState.HalfOpen:
                    await Close();
                    break;
            }
        }

        public async Task SignalFailure(TimeSpan expiry)
        {
            switch (stateDescriptor.State)
            {
                case CircuitBreakerState.Closed:
                    await Open(expiry);
                    break;
                case CircuitBreakerState.HalfOpen:
                    await Open(expiry);
                    break;
            }
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
