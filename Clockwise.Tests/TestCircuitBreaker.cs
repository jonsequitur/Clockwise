namespace Clockwise.Tests
{
    public class TestCircuitBreaker : CircuitBreaker<TestCircuitBreaker>
    {
        public TestCircuitBreaker(ICircuitBreakerBroker broker) : base(broker)
        {
        }
    }
}