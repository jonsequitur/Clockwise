using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerStorage
    {
        Task<CircuitBreakerStateDescriptor> GetLastStateAsync<T>() where T : CircuitBreaker<T>;
        Task SignalFailureAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>;
        Task SignalSuccessAsync<T>() where T : CircuitBreaker<T>;
        IDisposable Subscribe<T>(CircuitBreakerStateDescriptorSubscriber subscriber) where T : CircuitBreaker<T>;
    }

    public delegate void CircuitBreakerStateDescriptorSubscriber(CircuitBreakerStateDescriptor circuitBreakerStateDescriptor);
}