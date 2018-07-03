using System;
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
        public void
            When_circuitbreaker_is_not_configured_and_the_handler_requests_the_pause_then_usefull_exception_is_thrown()
        {

            var cfg = new Configuration()
                .UseInMemoryScheduling()
                .UseCircuitbreaker<int, TestCircuitBreaker>();

            new Action(() => { cfg.CommandReceiver<int>(); }).Should().Throw<ConfigurationException>();

        }

        [Fact]
        public async Task When_failure_occours_then_the_handler_can_signal_the_circuitbreaker()
        {
            using (var clock = VirtualClock.Start())
            {
                var processed = new List<int>();
                var cfg = new Configuration();
                cfg = cfg
                    .TraceCommands()
                    .UseDependency<ICircuitBreakerBroker>(type => new InMemoryCircuitBreakerBroker())
                    .UseInMemoryScheduling()
                    .UseCircuitbreaker<int, TestCircuitBreaker>();

                var handler = CommandHandler.Create<int>(delivery =>
                {
                    if (delivery.Command > 10)
                    {
                        return delivery.PauseAllDeliveriesFor(5.Seconds());
                    }

                    processed.Add(delivery.Command);
                    return delivery.Complete();
                });

                var rc = cfg.CommandReceiver<int>();
                rc.Subscribe(handler);

                var scheduler = cfg.CommandScheduler<int>();
                await scheduler.Schedule(1, 1.Seconds());
                await scheduler.Schedule(2, 2.Seconds());
                await scheduler.Schedule(11, 3.Seconds());
                await scheduler.Schedule(3, 4.Seconds());

                await clock.AdvanceBy(5.Seconds());

                processed.Should().BeEquivalentTo(1, 2);

                await clock.AdvanceBy(6.Seconds());

                processed.Should().BeEquivalentTo(1, 2, 3);
            }
        }

        [Fact]
        public async Task test_with_commandHandler()
        {
            using (var clock = VirtualClock.Start())
            {
                var processed = new List<int>();
                var cfg = new Configuration();
                cfg = cfg
                    .TraceCommands()
                    .UseDependency<ICircuitBreakerBroker>(type => new InMemoryCircuitBreakerBroker())
                    .UseInMemoryScheduling()
                    .UseHandlerDiscovery()
                    .UseCircuitbreaker<TestCommand, TestCircuitBreaker>();

                var scheduler = cfg.CommandScheduler<TestCommand>();
                await scheduler.Schedule( new TestCommand(1, processed), 1.Seconds());
                await scheduler.Schedule(new TestCommand(2, processed), 2.Seconds());
                await scheduler.Schedule(new TestCommand(11, processed), 3.Seconds());
                await scheduler.Schedule(new TestCommand(3, processed), 4.Seconds());

                await clock.AdvanceBy(5.Seconds());

                processed.Should().BeEquivalentTo(1, 2);

                await clock.AdvanceBy(6.Seconds());

                processed.Should().BeEquivalentTo(1, 2, 3);
            }
        }

        [Fact]
        public async Task When_failure_occours_then_no_handler_for_same_command_receive_messages()
        {
            using (var clock = VirtualClock.Start())
            {
                var processedInt = new List<int>();
                var processedLong = new List<long>();
                var cfg = new Configuration();
                cfg = cfg
                    .UseInMemeoryCircuitBreakerBroker()
                    .UseInMemoryScheduling()
                    .UseCircuitbreaker<int, TestCircuitBreaker>()
                    .UseCircuitbreaker<long, TestCircuitBreaker>();



                var intCommandHandler = CommandHandler.Create<int>(delivery =>
                {
                    if (delivery.Command > 10)
                    {
                        return delivery.PauseAllDeliveriesFor(10.Seconds());
                    }

                    processedInt.Add(delivery.Command);
                    return delivery.Complete();
                });

                var longCommandHandler = CommandHandler.Create<long>(delivery =>
                {
                    processedLong.Add(delivery.Command);
                    return delivery.Complete();
                });

                cfg.CommandReceiver<int>().Subscribe(intCommandHandler);
                cfg.CommandReceiver<long>().Subscribe(longCommandHandler);

                var intCommandscheduler = cfg.CommandScheduler<int>();
                await intCommandscheduler.Schedule(11); // open circuit!

                var longCommandScheduler = cfg.CommandScheduler<long>();
                await longCommandScheduler.Schedule(1, 1.Seconds());
                await longCommandScheduler.Schedule(2, 2.Seconds());
                await longCommandScheduler.Schedule(3, 3.Seconds());
                await clock.AdvanceBy(4.Seconds());

                processedLong.Should().BeEmpty();
            }
        }
    }

    public class TestCommandHandler : ICommandHandler<TestCommand>
    {
        public TestCommandHandler()
        {
            
        }
        public async  Task<ICommandDeliveryResult> Handle(ICommandDelivery<TestCommand> delivery)
        {
            await Task.Yield();
            if (delivery.Command.Payload > 10)
            {
                return delivery.PauseAllDeliveriesFor(5.Seconds());
            }
            delivery.Command.Processed.Add(delivery.Command.Payload);
            return delivery.Complete();
        }
    }

    public class TestCommand
    {
        public int Payload { get; }
        public List<int> Processed { get; }

        public TestCommand(int payload, List<int> processed)
        {
            Payload = payload;
            Processed = processed;
        }
    }
}