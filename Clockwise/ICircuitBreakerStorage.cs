using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerStorage : IObservable<CircuitBreakerStateDescriptor>
    {
        Task<CircuitBreakerStateDescriptor> GetLastStateAsync();
        Task SignalFailureAsync(TimeSpan expiry);
        Task SignalSuccessAsync();
    }
}