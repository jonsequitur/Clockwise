using System;

namespace Clockwise
{
    public interface ICircuitBreaker
    {
        CircuitBreakerState State { get; }

        CircuitBreakerStateDescriptor StateDescriptor { get; }
        void Open(TimeSpan? expiry = null);
        void HalfOpen();
        void Close();
    }

    public sealed class CircuitBraker : ICircuitBreaker
    {
        private readonly ICircuitBreakerStorage _storage;

        public CircuitBraker(ICircuitBreakerStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            StateDescriptor = _storage.GetStateAsync().Result;
            _storage.CircuitBreakerStateChanged += StorageOnCircuitBreakerStateChanged;
        }

        private void StorageOnCircuitBreakerStateChanged(object sender, CircuitBreakerStateDescriptor e)
        {
            StateDescriptor = e;
        }

        public CircuitBreakerState State => StateDescriptor.State;
        public CircuitBreakerStateDescriptor StateDescriptor { get; private set; }
        private void SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            _storage.SetState(newState, expiry);
        }

        public void Open(TimeSpan? expiry = null)
        {
            if (StateDescriptor.State != CircuitBreakerState.Open)
            {
                SetState(CircuitBreakerState.Open, expiry);
            }
        }

        public void HalfOpen()
        {
            if (StateDescriptor.State == CircuitBreakerState.Open)
            {
                SetState(CircuitBreakerState.HalfOpen);
            }
            else
            {
                throw new InvalidOperationException("Can transition to half open only from open state");
            }
        }

        public void Close()
        {
            if (StateDescriptor.State != CircuitBreakerState.Closed)
            {
                SetState(CircuitBreakerState.Closed);
            }
        }
    }
}
