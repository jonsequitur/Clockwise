using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerStorage : IDisposable
    {
        event EventHandler<CircuitBreakerStateDescriptor> CircuitBreakerStateChanged;
        Task<CircuitBreakerStateDescriptor> GetStateAsync();
        void SetState(CircuitBreakerState newState, TimeSpan? expiry = null);
    }
}