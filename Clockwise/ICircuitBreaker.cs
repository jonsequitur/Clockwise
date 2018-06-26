using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreaker : IDisposable
    {
        Task<CircuitBreakerStateDescriptor> GetLastStateAsync();
        Task SignalSuccess();
        Task SignalFailure(TimeSpan expiry);
    }
}