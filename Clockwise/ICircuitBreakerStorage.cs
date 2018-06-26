using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerStorage
    {
        Task<CircuitBreakerStateDescriptor> GetLastStateOfAsync<T>() where T : CircuitBreaker<T>;
        Task SignalFailureForAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>;
        Task SignalSuccessForAsync<T>() where T : CircuitBreaker<T>;
        IDisposable Subscribe<T>(IObserver<CircuitBreakerStateDescriptor> observer) where T : CircuitBreaker<T>;
    }
}