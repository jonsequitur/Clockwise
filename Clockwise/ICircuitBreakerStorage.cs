using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerStorage : IObservable<CircuitBreakerStateDescriptor>, IDisposable
    {
        Task<CircuitBreakerStateDescriptor> GetStateAsync();
        Task SetStateAsync(CircuitBreakerState newState, TimeSpan? expiry = null);
    }
}