using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerStorage : IObservable<CircuitBreakerStateDescriptor>, IDisposable
    {
        Task<CircuitBreakerStateDescriptor> GetStateAsync();
        void SetState(CircuitBreakerState newState, TimeSpan? expiry = null);
    }
}