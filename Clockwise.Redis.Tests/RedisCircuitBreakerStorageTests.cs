using System.Threading.Tasks;
using Clockwise.Tests;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis.Tests
{
    
    public class RedisCircuitBreakerStorageTests : CircuitBreakerStorageTests<ACICircuitBreaker>
    {
        
        protected override async Task<ICircuitBreakerStorage> CreateCircuitBreaker()
        {
            var settings = new RedisCircuitBreakerStorageSettings("127.0.0.1", 0);
            var cb01 = new CircuitBreakerStorage(settings);
            await cb01.InitialiseFor<ACICircuitBreaker>();
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