using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Clockwise.Tests
{
    public class ClockExtensionsTests
    {
        [Fact]
        public async Task VirtualClock_Wait_waits_until_the_specified_period_of_time_has_passed_and_scheduled_actions_are_triggered()
        {
            var receivedCount = 0;

            using (var clock = VirtualClock.Start())
            {
                clock.Schedule(_ => receivedCount++,
                               1.Seconds());
                clock.Schedule(_ => receivedCount++,
                               3.Seconds());

                await clock.Wait(2.Seconds());
            }

            receivedCount.Should().Be(1);
        }

        [Fact]
        public async Task RealtimeClock_Wait_waits_until_the_specified_period_of_time_has_passed_and_scheduled_actions_are_triggered()
        {
            var receivedCount = 0;

            var clock = new RealtimeClock();

            clock.Schedule(_ => receivedCount++,
                           1.Seconds());
            clock.Schedule(_ => receivedCount++,
                           3.Seconds());

            await clock.Wait(2.Seconds());

            receivedCount.Should().Be(1);
        }
    }
}
