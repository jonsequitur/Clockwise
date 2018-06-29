using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerBroker
    {
        Task<CircuitBreakerStateDescriptor> GetLastStateAsync<T>() where T : CircuitBreaker<T>;
        Task SignalFailureAsync<T>(TimeSpan expiry) where T : CircuitBreaker<T>;
        Task SignalSuccessAsync<T>() where T : CircuitBreaker<T>;
        IDisposable Subscribe<T>(CircuitBreakerBrokerSubscriber subscriber) where T : CircuitBreaker<T>;
        Task InitializeFor<T>() where T : CircuitBreaker<T>;
    }

    public delegate void CircuitBreakerBrokerSubscriber(CircuitBreakerStateDescriptor circuitBreakerStateDescriptor);
}