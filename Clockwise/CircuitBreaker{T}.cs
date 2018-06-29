using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class CircuitBreaker<T>
    where T : CircuitBreaker<T>
    {
        private ICircuitBreakerBroker broker;

        private CircuitBreakerStateDescriptor stateDescriptor;

        private void StateChanged(CircuitBreakerStateDescriptor descriptor) => stateDescriptor = descriptor;

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? broker.GetLastStateAsync<T>() : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess() => await broker.SignalSuccessAsync<T>();

        public async Task SignalFailure(TimeSpan expiry) => await broker.SignalFailureAsync<T>(expiry);

        internal void BindToBroker(ICircuitBreakerBroker circuitBreakerBroker)
        {
            broker = circuitBreakerBroker ?? throw new ArgumentNullException(nameof(circuitBreakerBroker));
            broker.Subscribe<T>(StateChanged);
        }
    }
}
