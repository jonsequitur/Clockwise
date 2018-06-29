using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class CircuitBreaker<T>
    where T : CircuitBreaker<T>
    {
        private readonly ICircuitBreakerBroker broker;

        private CircuitBreakerStateDescriptor stateDescriptor;

        protected CircuitBreaker(ICircuitBreakerBroker broker)
        {
            this.broker = broker ?? throw new ArgumentNullException(nameof(broker));
            this.broker.Subscribe<T>(StateChanged);
        }

        private void StateChanged(CircuitBreakerStateDescriptor descriptor) => stateDescriptor = descriptor;

        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? broker.GetLastStateAsync<T>() : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess() => await broker.SignalSuccessAsync<T>();

        public async Task SignalFailure(TimeSpan expiry) => await broker.SignalFailureAsync<T>(expiry);
    }
}
