using System;
using FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Clockwise.Tests
{
    public class InMemoryCommandBusTests
    {
        [Fact]
        public async Task When_a_command_is_undelivered_it_can_be_retrieved_from_the_bus()
        {
            using (var clock = VirtualClock.Start())
            {
                var bus = new InMemoryCommandBus<string>(clock);

                var handler = CommandHandler.Create<string>(cmd =>
                {
                    Console.WriteLine();
                });

                bus.Subscribe<string>(handler);

                await bus.Schedule("delivered",
                                   dueTime: clock.Now().AddDays(1));

                await bus.Schedule("undelivered",
                                   dueTime: clock.Now().AddDays(2));

                await clock.AdvanceBy(25.Hours());

                bus.Undelivered().Should().HaveCount(1);

                bus.Undelivered().Single().Command.Should().Be("undelivered");
            }
        }
    }
}
