using System;

namespace Clockwise
{
    public interface ICircuitBreakerStorage : IDisposable
    {
        event EventHandler<CircuitBreakerStateDescriptor> CircuitBreakerStateChanged;
        CircuitBreakerStateDescriptor GetState();
        void SetState(CircuitBreakerState newState, TimeSpan? expiry = null);
    }
}