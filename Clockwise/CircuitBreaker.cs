using System;

namespace Clockwise
{
    public interface ICircuitBreaker
    {
        CircuitBreakerState State { get; }

        CirtuitBreakerStateDescriptor StateDescriptor { get; }
        void SetState(CircuitBreakerState newState, TimeSpan? expiry = null);
    }

    public interface ICircuitBreakerStorage : IDisposable
    {
        event EventHandler<CirtuitBreakerStateDescriptor> CircuitBreakerStateChanged;
        CirtuitBreakerStateDescriptor StateDescriptor { get; }
        void SetState(CircuitBreakerState newState, TimeSpan? expiry = null);
    }

    public sealed class CircuitBraker : ICircuitBreaker
    {
        private readonly ICircuitBreakerStorage _storage;

        public CircuitBraker(ICircuitBreakerStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _storage.CircuitBreakerStateChanged += StorageOnCircuitBreakerStateChanged;
        }

        private void StorageOnCircuitBreakerStateChanged(object sender, CirtuitBreakerStateDescriptor e)
        {
            StateDescriptor = e;
        }

        public CircuitBreakerState State => StateDescriptor.State;
        public CirtuitBreakerStateDescriptor StateDescriptor { get; private set; }
        public void SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            _storage.SetState(newState, expiry);
        }
    }
}
