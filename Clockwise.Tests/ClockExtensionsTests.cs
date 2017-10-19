using System;
using System.Collections.Generic;
using FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
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

        [Fact]
        public async Task Repeat_runs_a_specified_action_repeatedly_at_the_specified_interval()
        {
            var log = new List<DateTimeOffset>();

            using (var clock = VirtualClock.Start(DateTimeOffset.Parse("1/1/2019 12:00:00 +00:00")))
            {
                clock.Repeat(async c =>
                {
                    log.Add(c.Now());
                }, () => 1.Days());

                await clock.AdvanceBy(10.Days());
            }

            log.Select(d => d.Day).Should().BeEquivalentTo(2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        }

        [Fact]
        public async Task Repeat_stops_running_when_disposed()
        {
            var log = new List<DateTimeOffset>();

            using (var clock = VirtualClock.Start(DateTimeOffset.Parse("1/1/2019 12:00:00 +00:00")))
            {
                var disposable = clock.Repeat(async c =>
                {
                    log.Add(c.Now());
                }, () => 1.Days());

                await clock.AdvanceBy(5.Days());

                disposable.Dispose();

                await clock.AdvanceBy(5.Days());
            }

            log.Select(d => d.Day).Should().BeEquivalentTo(2, 3, 4, 5, 6);
        }
    }
}
