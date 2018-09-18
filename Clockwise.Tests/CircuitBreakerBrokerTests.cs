using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using Pocket;
using Xunit;

namespace Clockwise.Tests
{
    public abstract class CircuitBreakerBrokerTests :  IDisposable
    {
        private readonly string circuitBreakerId = "testCircuitBreaker";
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        protected abstract Task<ICircuitBreakerBroker> CreateBroker(string circuitBreakerId);

        protected void AddToDisposable(IDisposable disposable)
        {
            disposables.Add(disposable);
        }

        [Fact]
        public async Task When_created_then_the_default_state_is_closed()
        {
            var cb01 = await CreateBroker(circuitBreakerId);
            var stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);
            stateDescriptor.Should().NotBeNull();
            stateDescriptor.State.Should().Be(CircuitBreakerState.Closed);
        }

        [Fact]
        public async Task When_a_closed_circuit_breaker_is_given_a_failure_signal_then_it_moves_to_open()
        {
            var cb01 = await CreateBroker(circuitBreakerId);
            var clock = Clock.Current;
            var stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);
            stateDescriptor.Should().NotBeNull();
            await cb01.SignalFailureAsync(circuitBreakerId, 2.Seconds());
            await clock.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);

            stateDescriptor.State.Should().Be(CircuitBreakerState.Open);
        }
      
        [Fact]
        public async Task When_an_open_circuit_breaker_is_given_a_success_signal_then_it_moves_to_half_open()
        {
            var cb01 = await CreateBroker(circuitBreakerId);
            var clock = Clock.Current;
            await cb01.SignalFailureAsync(circuitBreakerId, 2.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);
            stateDescriptor.State.Should().Be(CircuitBreakerState.Open);
            await clock.Wait(1.Seconds());
            await cb01.SignalSuccessAsync(circuitBreakerId);
            await clock.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);

            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
        }

        [Fact]
        public async Task When_a_half_open_circuit_breaker_is_given_a_success_signal_then_it_moves_to_closed()
        {
            var cb01 = await CreateBroker(circuitBreakerId);
            var clock = Clock.Current;
            await cb01.SignalFailureAsync(circuitBreakerId, TimeSpan.FromSeconds(2));
            await clock.Wait(1.Seconds());
            await cb01.SignalSuccessAsync(circuitBreakerId);
            await clock.Wait(1.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
            await cb01.SignalSuccessAsync(circuitBreakerId);
            await clock.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);

            stateDescriptor.State.Should().Be(CircuitBreakerState.Closed);
        }
        [Fact]
        public async Task When_a_half_open_circuit_breaker_is_given_a_failure_signal_then_it_moves_to_open()
        {
            var cb01 = await CreateBroker(circuitBreakerId);
            var clock = Clock.Current;
            await cb01.SignalFailureAsync(circuitBreakerId, 2.Seconds());
            await clock.Wait(1.Seconds());
            await cb01.SignalSuccessAsync(circuitBreakerId);
            await clock.Wait(1.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);
            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
            await cb01.SignalFailureAsync(circuitBreakerId, 2.Seconds());
            await clock.Wait(1.Seconds());
            stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);

            stateDescriptor.State.Should().Be(CircuitBreakerState.Open);
        }

        [Fact]
        public async Task When_open_state_expires_it_moves_to_half_open()
        {
            var cb01 = await CreateBroker(circuitBreakerId);
            var clock = Clock.Current;
            await cb01.SignalFailureAsync(circuitBreakerId, 1.Seconds());
            await clock.Wait(2.Seconds());
            var stateDescriptor = await cb01.GetLastStateAsync(circuitBreakerId);

            stateDescriptor.State.Should().Be(CircuitBreakerState.HalfOpen);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}