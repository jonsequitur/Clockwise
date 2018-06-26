using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using Pocket;
using Xunit;

namespace Clockwise.Tests
{
    public abstract class CircuitBreakerStorageTests : IDisposable
    {
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        protected abstract Task<ICircuitBreakerStorage> CreateCircuitBreaker();

        protected abstract IClock GetClock();
        protected void AddToDisposable(IDisposable disposable)
        {
            disposables.Add(disposable);
        }

        [Fact]
        public async Task When_signaling_failure_then_state_is_open()
        {
            var cb01 = await CreateCircuitBreaker();
            var clock = GetClock();
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.Should().NotBeNull();
            await cb01.SignalFailureAsync(2.Seconds());
            await clock.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.Open);
        }

      
        [Fact]
        public async Task Give_an_open_circuitbreaker_then_signaling_succes_then_state_is_half_open()
        {
            var cb01 = await CreateCircuitBreaker();
            var clock = GetClock();
            await cb01.SignalFailureAsync(2.Seconds());
            await clock.Wait(1.Seconds());
            await cb01.SignalSuccessAsync();
            await clock.Wait(1.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
        }

        [Fact]
        public async Task Give_an_half_open_circuitbreaker_then_signaling_succes_then_state_is_closed()
        {
            var cb01 = await CreateCircuitBreaker();
            var clock = GetClock();
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
            var cb01 = await CreateCircuitBreaker();
            var clock = GetClock();
            await cb01.SignalFailureAsync(2.Seconds());
            await clock.Wait(1.Seconds());
            await cb01.SignalSuccessAsync();
            await clock.Wait(1.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
            await cb01.SignalFailureAsync(2.Seconds());
            await clock.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.Open);
        }

        [Fact]
        public async Task When_open_state_expires_it_is_set_to_half_open()
        {
            var cb01 = await CreateCircuitBreaker();
            var clock = GetClock();
            await cb01.SignalFailureAsync(1.Seconds());
            await clock.Wait(2.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync();
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}