namespace Clockwise.Tests
{
    public class TestCircuitBreaker : CircuitBreaker<TestCircuitBreaker>
    {
        public TestCircuitBreaker(ICircuitBreakerStorage storage) : base(storage)
        {
        }
    }
}