using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Clockwise.Tests
{
    public class CircuitBreakerTests
    {
        [Fact]
        public async Task CircuitBreaker_can_be_use_to_intercept_delivery()
        {
            var processed = new List<int>();
            var cb = new CircuitBraker(new InMemoryCircuitBreakerStorage());
            var handler = CommandHandler.Create<int>(async delivery =>
                {
                    if (delivery.Command > 10)
                    {
                        await cb.SignalFailure();
                    }
                    else
                    {
                        processed.Add(delivery.Command);
                    }
                })
                .UseMiddleware(async (delivery, next) =>
                {
                    if (cb.StateDescriptor.State == CircuitBreakerState.Closed)
                    {
                        return await next(delivery);
                    }

                    return null;
                });

            await handler.Handle(new CommandDelivery<int>(1));
            await handler.Handle(new CommandDelivery<int>(2));
            await handler.Handle(new CommandDelivery<int>(11));
            await handler.Handle(new CommandDelivery<int>(3));

            processed.Should().BeEquivalentTo(1, 2);
        }

        [Fact]
        public async Task CircuitBreaker_once_closed_lets_delivery_go_though()
        {
            var processed = new List<int>();
            var cb = new CircuitBraker(new InMemoryCircuitBreakerStorage());
            var handler = CommandHandler.Create<int>(async delivery =>
                {
                    if (delivery.Command > 10)
                    {
                       await cb.SignalFailure();
                    }
                    else
                    {
                        processed.Add(delivery.Command);
                    }
                })
                .UseMiddleware(async (delivery, next) =>
                {
                    if (cb.StateDescriptor.State == CircuitBreakerState.Closed)
                    {
                        return await next(delivery);
                    }

                    return null;
                });

            await handler.Handle(new CommandDelivery<int>(1));
            await handler.Handle(new CommandDelivery<int>(2));
            await handler.Handle(new CommandDelivery<int>(11));
            await handler.Handle(new CommandDelivery<int>(3));

            processed.Should().BeEquivalentTo(1, 2);

            await cb.Close();
            await handler.Handle(new CommandDelivery<int>(3));

            processed.Should().BeEquivalentTo(1, 2, 3);
        }

        [Fact]
        public async  Task Using_scheduler_open_state_can_be_reverted_in_the_future()
        {
            using (var clock = VirtualClock.Start())
            {
                var cfg = new Configuration();
                cfg.UseInMemoryScheduling();
                var cb = new CircuitBraker(new InMemoryCircuitBreakerStorage(),cfg);
                await cb.Open(9.Seconds());
                cb.StateDescriptor.State.Should().Be(CircuitBreakerState.Open);
                await clock.AdvanceBy(11.Seconds());
                cb.StateDescriptor.State.Should().Be(CircuitBreakerState.Closed);
            }
        }
    }
}