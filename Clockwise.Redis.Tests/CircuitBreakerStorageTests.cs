using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using StackExchange.Redis;
using Xunit;

namespace Clockwise.Redis.Tests
{
    public class CircuitBreakerStorageTests : IDisposable
    {
        [Fact]
        public async Task When_signaling_failure_then_state_is_open()
        {
            var cb01 = new CircuitBreakerStorage("127.0.0.1", 0,typeof(string));
            await cb01.Initialise();
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.Should().NotBeNull();
            await cb01.SignalFailureAsync(2.Seconds());
            await Clock.Current.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.Open);
        }

        [Fact]
        public async Task Give_an_open_circuitbreaker_then_signaling_succes_then_state_is_half_open()
        {
            var cb01 = new CircuitBreakerStorage("127.0.0.1", 0, typeof(string));
            await cb01.Initialise();
            await cb01.SignalFailureAsync(2.Seconds());
            await Clock.Current.Wait(1.Seconds());
            await cb01.SignalSuccessAsync();
            await Clock.Current.Wait(1.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
        }

        [Fact]
        public async Task Give_an_half_open_circuitbreaker_then_signaling_succes_then_state_is_closed()
        {
            var cb01 = new CircuitBreakerStorage("127.0.0.1", 0, typeof(string));
            await cb01.Initialise();
           await cb01.SignalFailureAsync(TimeSpan.FromSeconds(2));
            await Clock.Current.Wait(1.Seconds());
            await cb01.SignalSuccessAsync();
            await Clock.Current.Wait(1.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
            await cb01.SignalSuccessAsync();
            await Clock.Current.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.Closed);
        }
        [Fact]
        public async Task Give_an_half_open_circuitbreaker_then_signaling_failure_then_state_is_closed()
        {
            var cb01 = new CircuitBreakerStorage("127.0.0.1", 0, typeof(string));
            await cb01.Initialise();
            await cb01.SignalFailureAsync(2.Seconds());
            await Clock.Current.Wait(1.Seconds());
            await cb01.SignalSuccessAsync();
            await Clock.Current.Wait(1.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
            await cb01.SignalFailureAsync(2.Seconds());
            await Clock.Current.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.Open);
        }

        [Fact]
        public async Task When_open_state_expires_it_is_set_to_half_open()
        {
            var cb01 = new CircuitBreakerStorage("127.0.0.1", 0, typeof(string));
            await cb01.Initialise();
            await cb01.SignalFailureAsync(1.Seconds());
            await Clock.Current.Wait(2.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
        }

        public void Dispose()
        {
          var connection =  ConnectionMultiplexer.Connect("127.0.0.1");
            connection.GetDatabase().Execute("FLUSHALL");
        }
    }
}