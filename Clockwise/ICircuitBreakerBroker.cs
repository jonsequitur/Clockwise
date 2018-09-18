using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerBroker
    {
        Task<CircuitBreakerStateDescriptor> GetLastStateAsync(string circuitBreakerId);
        Task SignalFailureAsync(string circuitBreakerId, TimeSpan expiry);
        Task SignalSuccessAsync(string circuitBreakerId);
        Task SubscribeAsync(string circuitBreakerId, CircuitBreakerBrokerSubscriber subscriber);
        Task InitializeAsync(string circuitBreakerId);
    }

    // FIX: (CircuitBreakerBrokerSubscriber)  rename
    public delegate void CircuitBreakerBrokerSubscriber(CircuitBreakerStateDescriptor circuitBreakerStateDescriptor);
}