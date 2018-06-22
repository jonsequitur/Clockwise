using System;

namespace Clockwise
{
    public interface ICircuitBreaker
    {
        CircuitBreakerState State { get; }

        CircuitBreakerStateDescriptor StateDescriptor { get; }
        void SetState(CircuitBreakerState newState, TimeSpan? expiry = null);
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
        public void SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            _storage.SetState(newState, expiry);
        }
    }
}
