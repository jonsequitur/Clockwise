using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Clockwise.Tests
{
    public class ACICircuitBreaker : CircuitBreaker<ACICircuitBreaker>
    {
        public ACICircuitBreaker(ICircuitBreakerStorage storage, Configuration pocketConfiguration = null) : base(storage, pocketConfiguration)
        {
        }
    }
    public class CircuitBreakerTests
    {
        [Fact]
        public async Task When_circuitbreaker_is_not_configured_and_the_handler_requests_the_pause_then_usefull_exception_is_thrown()
        {
            using (var clock = VirtualClock.Start())
            {
                var cfg = new Configuration()
                    .UseInMemoryScheduling()
                    .UseCircuitbreakerFor<int, ACICircuitBreaker>();


                var handler = CommandHandler.Create<int>(delivery =>
                {
                    if (delivery.Command > 10)
                    {
                        return delivery.PauseAllDeliveriesFor(1.Seconds());
                    }

                    return delivery.Complete();
                });

                new Action(() =>
                {
                    cfg.CommandReceiver<int>().Subscribe(handler);
                }).Should().Throw<CircuitBreakerException>();
            }
        }

        [Fact]
        public async Task When_failure_occours_then_the_handler_can_signal_the_circuitbreaker()
        {
            using (var clock = VirtualClock.Start())
            {
                var processed = new List<int>();
                var cfg = new Configuration();
                cfg = cfg
                    .UseDependency<ICircuitBreakerStorage>(type => new InMemoryCircuitBreakerStorage())
                    .UseInMemoryScheduling()
                   .UseCircuitbreakerFor<int, ACICircuitBreaker>();

                

                var handler = CommandHandler.Create<int>(delivery =>
                {
                    if (delivery.Command > 10)
                    {
                        return delivery.PauseAllDeliveriesFor(1.Seconds());
                    }

                    processed.Add(delivery.Command);
                    return delivery.Complete();
                });

                var rc = cfg.CommandReceiver<int>();
                 rc.Subscribe(handler);
                
                var scheduler = cfg.CommandScheduler<int>();
                await scheduler.Schedule(1);
                await scheduler.Schedule(2);
                await scheduler.Schedule(11);
                await scheduler.Schedule(3);

                await clock.AdvanceBy(10.Minutes());

                processed.Should().BeEquivalentTo(1, 2);
            }
        }

        [Fact]
        public void When_failure_occours_then_no_handler_for_same_command_receive_messages()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task CircuitBreaker_can_be_use_to_intercept_delivery()
        {
            var processed = new List<int>();
            var cb = new ACICircuitBreaker(new InMemoryCircuitBreakerStorage());
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
            var cb = new ACICircuitBreaker(new InMemoryCircuitBreakerStorage());
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
                var cb = new ACICircuitBreaker(new InMemoryCircuitBreakerStorage(),cfg);
                await cb.Open(9.Seconds());
                cb.StateDescriptor.State.Should().Be(CircuitBreakerState.Open);
                await clock.AdvanceBy(11.Seconds());
                cb.StateDescriptor.State.Should().Be(CircuitBreakerState.Closed);
            }
        }
    }
}