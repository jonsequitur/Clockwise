using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerStorage : IObservable<CircuitBreakerStateDescriptor>
    {
        Task SetStateAsync(CircuitBreakerState newState, TimeSpan? expiry = null);
    }
}