using System;
using System.Threading.Tasks;
using FluentAssertions;
using StackExchange.Redis;
using Xunit;

namespace Clockwise.Redis.Tests
{
    public class CircuitBreakerStorageTests : IDisposable
    {
        [Fact]
        public async Task SingalingState()
        {
            var cb01 = new CircuitBreakerStorage("127.0.0.1", 0,typeof(string));
            await cb01.Initialise();
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.Should().NotBeNull();
            await cb01.SetStateAsync(CircuitBreakerState.HalfOpen, TimeSpan.FromMinutes(1));
            await Task.Delay(1000);
            stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
        }

        public void Dispose()
        {
          var connection =  ConnectionMultiplexer.Connect("127.0.0.1");
            connection.GetDatabase().Execute("FLUSHALL");
        }
    }
}