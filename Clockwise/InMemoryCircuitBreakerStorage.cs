using System;

namespace Clockwise
{
    public sealed class InMemoryCircuitBreakerStorage : ICircuitBreakerStorage
    {
        public InMemoryCircuitBreakerStorage()
        {
            StateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now());
        }

        public void Dispose()
        {
        }

        public event EventHandler<CircuitBreakerStateDescriptor> CircuitBreakerStateChanged;
        public CircuitBreakerStateDescriptor StateDescriptor { get; private set; }

        public void SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            var desc = new CircuitBreakerStateDescriptor(newState, Clock.Current.Now(), expiry);
            if (desc != StateDescriptor)
            {
                StateDescriptor = desc;
                CircuitBreakerStateChanged?.Invoke(this, StateDescriptor);
            }
        }
    }
}