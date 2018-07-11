using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerBroker
    {
        Task<CircuitBreakerStateDescriptor> GetLastStateAsync(string circuitBreakerId);
        Task SignalFailureAsync(string circuitBreakerId, TimeSpan expiry);
        Task SignalSuccessAsync(string circuitBreakerId);
        void Subscribe(string circuitBreakerId, CircuitBreakerBrokerSubscriber subscriber);
        Task InitializeFor(string circuitBreakerId);
    }

    public delegate void CircuitBreakerBrokerSubscriber(CircuitBreakerStateDescriptor circuitBreakerStateDescriptor);
}