using System.Threading.Tasks;

namespace Clockwise.Tests
{
    public class InMemoryCircuitBreakerBrokerTests : CircuitBreakerBrokerTests
    {
        protected override async Task<ICircuitBreakerBroker> CreateBroker(string circuitBreakerId)
        {
            ICircuitBreakerBroker cb = new InMemoryCircuitBreakerBroker();
            await cb.InitializeFor(circuitBreakerId);
            return cb;
        }

        protected override IClock GetClock()
        {
            return Clock.Current;
        }
    }
}