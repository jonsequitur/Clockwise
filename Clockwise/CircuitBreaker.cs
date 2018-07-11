using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public class CircuitBreaker
    {
        private ICircuitBreakerBroker broker;

        private CircuitBreakerStateDescriptor stateDescriptor;
        public string Id { get; }

        public CircuitBreaker(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(id));
            }
            Id = id;
        }

        private void StateChanged(CircuitBreakerStateDescriptor descriptor) => stateDescriptor = descriptor;

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? broker.GetLastStateAsync(Id) : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess() => await broker.SignalSuccessAsync(Id);

        public async Task SignalFailure(TimeSpan expiry) => await broker.SignalFailureAsync(Id, expiry);

        public void BindToBroker(ICircuitBreakerBroker circuitBreakerBroker)
        {
            broker = circuitBreakerBroker ?? throw new ArgumentNullException(nameof(circuitBreakerBroker));
            broker.Subscribe(Id, StateChanged);
        }
    }
}
