using System;
using FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using Xunit;

namespace Clockwise.Tests
{
    public class InMemoryCommandBusTests
    {
        [Fact]
        public async Task When_a_command_is_undelivered_it_can_be_retrieved_from_the_bus_before_it_is_due()
        {
            using (var clock = VirtualClock.Start())
            {
                var bus = new InMemoryCommandBus<string>(clock);

                await bus.Schedule("undelivered",
                                   dueTime: clock.Now().AddDays(2));

                bus.Undelivered().Should().HaveCount(1);

                bus.Undelivered().Single().Command.Should().Be("undelivered");
            }
        }

        [Fact]
        public async Task When_a_command_is_undelivered_it_can_be_retrieved_from_the_bus_after_it_is_due()
        {
            using (var clock = VirtualClock.Start())
            {
                var bus = new InMemoryCommandBus<string>(clock);

                await bus.Schedule("undelivered",
                                   1.Seconds());

                await clock.AdvanceBy(3.Days());

                bus.Undelivered().Should().HaveCount(1);

                bus.Undelivered().Single().Command.Should().Be("undelivered");
            }
        }

        [Fact]
        public async Task Intervening_scheduled_actions_do_not_prevent_Receive_from_waiting_the_entire_timeout()
        {
            var received = false;
            using (var configuration = new Configuration()
                .TraceCommands()
                .UseDependency<IStore<CommandTarget>>(_ => new InMemoryStore<CommandTarget>())
                .UseHandlerDiscovery()
                .UseInMemoryScheduling())
            {
                await configuration
                    .CommandScheduler<CreateCommandTarget>()
                    .Schedule(new CreateCommandTarget("id"), 1.Seconds());

                await configuration
                    .CommandScheduler<string>()
                    .Schedule("hi!",
                              2.Seconds());

                var receiver = configuration.CommandReceiver<string>();

                await receiver.Receive(async d =>
                {
                    received = true;
                    return d.Complete();
                }, 3.Seconds());
            }

            received.Should().BeTrue();
        }
    }
}
