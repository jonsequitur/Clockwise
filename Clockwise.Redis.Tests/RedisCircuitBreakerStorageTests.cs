using System.Threading.Tasks;
using Clockwise.Tests;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis.Tests
{
    
    public class RedisCircuitBreakerStorageTests : CircuitBreakerStorageTests<TestCircuitBreaker>
    {
        
        protected override async Task<ICircuitBreakerBroker> CreateCircuitBreaker()
        {
            var cb01 = new CircuitBreakerBroker("127.0.0.1", 0);
            await cb01.InitializeFor<TestCircuitBreaker>();
            AddToDisposable(Disposable.Create(() =>
            {
                var connection = ConnectionMultiplexer.Connect("127.0.0.1");
                connection.GetDatabase().Execute("FLUSHALL");
                cb01.Dispose();
                connection.Dispose();
            }));
            return cb01;
        }

        protected override IClock GetClock()
        {
            return Clock.Current;
        }
    }
}