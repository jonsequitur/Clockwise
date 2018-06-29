using System.Threading.Tasks;

namespace Clockwise.Tests
{
    public class InMemoryCircuitBreakerBrokerTests : CircuitBreakerBrokerTests<TestCircuitBreaker>
    {
        protected override Task<ICircuitBreakerBroker> CreateBroker()
        {
            ICircuitBreakerBroker cb = new InMemoryCircuitBreakerBroker();
            return Task.FromResult(cb);
        }

        protected override IClock GetClock()
        {
            return Clock.Current;
        }
    }
}