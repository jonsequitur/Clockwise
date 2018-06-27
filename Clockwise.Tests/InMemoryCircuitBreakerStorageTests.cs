using System.Threading.Tasks;

namespace Clockwise.Tests
{
    public class InMemoryCircuitBreakerStorageTests : CircuitBreakerStorageTests<TestCircuitBreaker>
    {
        protected override Task<ICircuitBreakerStorage> CreateCircuitBreaker()
        {
            ICircuitBreakerStorage cb = new InMemoryCircuitBreakerStorage();
            return Task.FromResult(cb);
        }

        protected override IClock GetClock()
        {
            return Clock.Current;
        }
    }
}