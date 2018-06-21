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
        public void SingalingState()
        {
            var cb01 = CircuitBreakerStorage.Create<string>("127.0.0.1");
            cb01.StateDescriptor.State.Should().Be(CircuitBreakerState.Closed);
            cb01.SetState(CircuitBreakerState.HalfClosed);
            Task.Delay(100).Wait();
            cb01.StateDescriptor.State.Should().Be(CircuitBreakerState.HalfClosed);
        }

        public void Dispose()
        {
          var connection =  ConnectionMultiplexer.Connect("127.0.0.1");
            connection.GetDatabase().Execute("FLUSHALL");
        }
    }
}