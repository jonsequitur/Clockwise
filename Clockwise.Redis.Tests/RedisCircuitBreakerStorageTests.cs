using System.Threading.Tasks;
using Clockwise.Tests;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis.Tests
{
    public class RedisCircuitBreakerStorageTests : CircuitBreakerStorageTests
    {
        protected override async Task<ICircuitBreakerStorage> CreateCircuitBreaker()
        {
            var cb01 = new CircuitBreakerStorage("127.0.0.1", 0, typeof(string));
            await cb01.Initialise();
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