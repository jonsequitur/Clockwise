using System;

namespace Clockwise
{
    public interface ICircuitBreakerStorage : IDisposable
    {
        event EventHandler<CirtuitBreakerStateDescriptor> CircuitBreakerStateChanged;
        CirtuitBreakerStateDescriptor StateDescriptor { get; }
        void SetState(CircuitBreakerState newState, TimeSpan? expiry = null);
    }
}