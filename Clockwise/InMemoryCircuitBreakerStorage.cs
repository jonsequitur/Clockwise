using System;

namespace Clockwise
{
    public sealed class InMemoryCircuitBreakerStorage : ICircuitBreakerStorage
    {
        private CircuitBreakerStateDescriptor stateDescriptor;
        public InMemoryCircuitBreakerStorage()
        {
            stateDescriptor = new CircuitBreakerStateDescriptor(CircuitBreakerState.Closed, Clock.Current.Now());
        }

        public void Dispose()
        {
        }

        public event EventHandler<CircuitBreakerStateDescriptor> CircuitBreakerStateChanged;
        

        public CircuitBreakerStateDescriptor GetState()
        {
            return stateDescriptor;
        }

        public void SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            var desc = new CircuitBreakerStateDescriptor(newState, Clock.Current.Now(), expiry);
            if (desc != stateDescriptor)
            {
                stateDescriptor = desc;
                CircuitBreakerStateChanged?.Invoke(this, stateDescriptor);
            }
        }
    }
}