using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Pocket;
using Xunit;

namespace Clockwise.Tests
{
    public class VirtualClockTests
    {
        [Fact]
        public void Clock_can_be_overridden_using_VirtualClock()
        {
            var virtualTime = DateTimeOffset.Parse("6/2/2027 12:23am");

            using (VirtualClock.Start(virtualTime))
            {
                Clock.Now().Should().Be(virtualTime);
            }
        }

        [Fact]
        public async Task Instantiating_a_VirtualClock_freezes_time()
        {
            var virtualTime = DateTimeOffset.Parse("6/2/2027 12:23am");

            using (VirtualClock.Start(virtualTime))
            {
                await Task.Delay(200);

                Clock.Now().Should().Be(virtualTime);
            }
        }

        [Fact]
        public async Task The_clock_can_be_advanced_to_a_specific_time()
        {
            var initialTime = DateTimeOffset.Parse("6/15/2017 1:00pm");
            var newTime = DateTimeOffset.Parse("6/16/2018 1:50pm");

            using (var clock = VirtualClock.Start(initialTime))
            {
                await clock.AdvanceTo(newTime);

                Clock.Now().Should().Be(newTime);
            }
        }

        [Fact]
        public async Task The_clock_can_be_advanced_by_a_specific_timespan()
        {
            var initialTime = DateTimeOffset.Parse("6/15/2017 1:00pm");

            using (var clock = VirtualClock.Start(initialTime))
            {
                await clock.AdvanceBy(12.Minutes());

                Clock.Now().Should().Be(initialTime + 12.Minutes());
            }
        }

        [Fact]
        public void The_virtual_clock_cannot_be_moved_backwards_using_AdvanceBy()
        {
            using (var clock = VirtualClock.Start())
            {
                Func<Task> moveBackwards = () => clock.AdvanceBy(-1.Minutes());

                moveBackwards.ShouldThrow<ArgumentException>()
                             .Which
                             .Message
                             .Should()
                             .Be("The clock cannot be moved backward in time.");
            }
        }

        [Fact]
        public void The_virtual_clock_cannot_be_moved_backwards_using_AdvanceTo()
        {
            using (var clock = VirtualClock.Start())
            {
                Func<Task> moveBackwards = () => clock.AdvanceTo(clock.Now().Subtract(1.Minutes()));

                moveBackwards.ShouldThrow<ArgumentException>()
                             .Which
                             .Message
                             .Should()
                             .Be("The clock cannot be moved backward in time.");
            }
        }

        [Fact]
        public async Task Schedule_can_specify_actions_that_are_invoked_when_the_virtual_clock_is_advanced()
        {
            var actions = new List<string>();

            using (var clock = VirtualClock.Start())
            {
                clock.Schedule(_ => actions.Add("one"), 2.Seconds());
                clock.Schedule(_ => actions.Add("two"), 1.Hours());

                await clock.AdvanceBy(3.Seconds());

                actions.ShouldBeEquivalentTo(new[] { "one" });

                await clock.AdvanceBy(3.Hours());

                actions.ShouldBeEquivalentTo(new[] { "one", "two" });
            }
        }

        [Fact]
        public async Task A_schedule_action_can_read_the_current_time_from_the_schedule()
        {
            var events = new List<DateTimeOffset>();

            var startTime = DateTimeOffset.Parse("1/1/2018 1:00pm +00:00");

            using (var clock = VirtualClock.Start(startTime))
            {
                clock.Schedule(c => events.Add(c.Now()), 2.Seconds());
                clock.Schedule(c => events.Add(c.Now()), 3.Seconds());
                clock.Schedule(c => events.Add(c.Now()), 1.Hours());

                await clock.AdvanceBy(20.Days());
            }

            events.ShouldBeEquivalentTo(new[]
            {
                startTime.Add(2.Seconds()),
                startTime.Add(3.Seconds()),
                startTime.Add(1.Hours())
            });
        }

        [Fact]
        public async Task A_scheduled_action_can_schedule_additional_actions()
        {
            DateTimeOffset secondActionExecutedAt;
            var startTime = DateTimeOffset.Parse("1/1/2018 1:00pm +00:00");

            using (var clock = VirtualClock.Start(startTime))
            {
                clock.Schedule(
                    c => c.Schedule(
                        c2 => secondActionExecutedAt = c2.Now(),
                        2.Minutes()),
                    1.Minutes());

                await clock.AdvanceBy(5.Minutes());
            }

            secondActionExecutedAt
                .Should()
                .Be(startTime.Add(3.Minutes()));
        }

        [Fact]
        public async Task A_schedule_action_can_be_scheduled_for_as_soon_as_possible_by_not_specifying_a_due_time()
        {
            var events = new List<DateTimeOffset>();

            var startTime = DateTimeOffset.Parse("1/1/2018 1:00pm +00:00");

            using (var clock = VirtualClock.Start(startTime))
            {
                clock.Schedule(c => events.Add(c.Now()));
                clock.Schedule(c => events.Add(c.Now()));
                clock.Schedule(c => events.Add(c.Now()));

                await clock.AdvanceBy(1.Seconds());

                events.ShouldBeEquivalentTo(new[]
                {
                    startTime.AddTicks(1),
                    startTime.AddTicks(2),
                    startTime.AddTicks(3)
                });
            }
        }

        [Fact]
        public async Task When_the_clock_is_advanced_then_scheduled_async_actions_are_awaited()
        {
            var done = false;

            using (var clock = VirtualClock.Start())
            {
                clock.Schedule(async s =>
                {
                    await Task.Delay(20);

                    done = true;
                });

                await clock.AdvanceBy(5.Seconds());

                done.Should().BeTrue();
            }
        }

        [Fact]
        public void When_one_virtual_clock_is_active_another_cannot_be_started()
        {
            using (VirtualClock.Start())
            {
                Action startAnother = () => VirtualClock.Start();

                startAnother.ShouldThrow<InvalidOperationException>()
                            .Which
                            .Message
                            .Should()
                            .Be("A virtual clock cannot be started while another is still active in the current context.");
            }
        }

        [Fact]
        public void When_actions_are_scheduled_in_the_future_then_TimeUntilNextActionIsDue_returns_the_expected_time()
        {
            using (var clock = VirtualClock.Start())
            {
                clock.Schedule(_ =>
                {
                }, 1.Minutes());

                clock.Schedule(_ =>
                {
                }, 1.Seconds());

                clock.Schedule(_ =>
                {
                }, 1.Hours());

                clock.TimeUntilNextActionIsDue.Should().Be(1.Seconds());
            }
        }

        [Fact]
        public async Task When_actions_were_scheduled_in_the_past_and_are_scheduled_in_the_future_then_TimeUntilNextActionIsDue_returns_the_expected_time()
        {
            using (var clock = VirtualClock.Start())
            {
                clock.Schedule(_ =>
                {
                }, 1.Minutes());

                clock.Schedule(_ =>
                {
                }, 1.Seconds());

                clock.Schedule(_ =>
                {
                }, 1.Hours());

                await clock.AdvanceBy(1.Seconds());

                clock.TimeUntilNextActionIsDue.Should().Be(1.Minutes() - 1.Seconds());
            }
        }

        [Fact]
        public void When_no_actions_have_been_scheduled_then_TimeUntilNextActionIsDue_returns_null()
        {
            using (var clock = VirtualClock.Start())
            {
                clock.TimeUntilNextActionIsDue.Should().BeNull();
            }
        }

        [Fact]
        public void VirtualClock_logs_the_time_on_start()
        {
            var startTime = DateTimeOffset.Parse("9/2/2017 12:03:04pm");
            var log = new List<string>();

            using (LogEvents.Subscribe(e => log.Add(e.ToLogString())))
            using (VirtualClock.Start(startTime))
            {
                log.Single().Should().Match($"*[Clockwise.VirtualClock]*Starting at {startTime}*");
            }
        }

        [Fact]
        public async Task When_advanced_it_logs_the_time_and_ticks_at_start_and_stop_of_operation()
        {
            var startTime = DateTimeOffset.Parse("9/2/2017 12:03:04pm");
            var log = new List<string>();

            using (LogEvents.Subscribe(e => log.Add(e.ToLogString())))
            using (var clock = VirtualClock.Start(startTime))
            {
                await clock.AdvanceBy(1.Milliseconds());

                log[1].Should().Match($"*[Clockwise.VirtualClock] [AdvanceTo]  ▶ Advancing from {startTime} ({startTime.Ticks}) to {clock.Now()} ({clock.Now().Ticks})*");
                log[2].Should().Match($"*[Clockwise.VirtualClock] [AdvanceTo]  ⏹ -> ✔ (*ms)  +[ (nowAt, {Clock.Now()}) ]*");
            }
        }
    }
}
