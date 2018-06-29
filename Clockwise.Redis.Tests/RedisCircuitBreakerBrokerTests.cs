using System.Threading.Tasks;
using Clockwise.Tests;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis.Tests
{
    
    public class RedisCircuitBreakerBrokerTests : CircuitBreakerBrokerTests<TestCircuitBreaker>
    {
        protected override async Task<ICircuitBreakerBroker> CreateBroker()
        {
            var db = 1;
            var cb01 = new CircuitBreakerBroker("127.0.0.1", db);
            AddToDisposable(Disposable.Create(() =>
            {
                var connection = ConnectionMultiplexer.Connect("127.0.0.1");
                connection.GetDatabase().Execute("FLUSHALL");
                cb01.Dispose();
                connection.Dispose();
            }));
            await cb01.InitializeFor<TestCircuitBreaker>();
            return cb01;
        }

        protected override IClock GetClock()
        {
            return Clock.Current;
        }
    }
}