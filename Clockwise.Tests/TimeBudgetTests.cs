using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using static System.Environment;

namespace Clockwise.Tests
{
    public class TimeBudgetTests
    {
        [Fact]
        public void When_the_budget_is_created_then_the_start_time_is_captured()
        {
            var startTime = DateTimeOffset.Parse(
                "2017-01-01 12:00am +00:00",
                CultureInfo.InvariantCulture);

            using (var clock = VirtualClock.Start(startTime))
            {
                var budget = new TimeBudget(clock, 5.Seconds());

                budget.StartTime.Should().Be(clock.Now());
            }
        }

        [Fact]
        public async Task The_remaining_duration_can_be_checked()
        {
            using (var clock = VirtualClock.Start())
            {
                var budget = new TimeBudget(clock, 5.Seconds());

                await clock.AdvanceBy(3.Seconds());

                budget.RemainingDuration.Should().Be(2.Seconds());
            }
        }

        [Fact]
        public async Task When_the_clock_is_advanced_then_start_time_does_not_change()
        {
            var startTime = DateTimeOffset.Parse(
                "2017-01-01 12:00am +00:00",
                CultureInfo.InvariantCulture);

            using (var clock = VirtualClock.Start(startTime))
            {
                var budget = new TimeBudget(clock, 5.Seconds());

                await clock.AdvanceBy(5.Minutes());

                budget.StartTime.Should().Be(startTime);
            }
        }

        [Fact]
        public void TimeBudget_cannot_be_created_with_a_timespan_of_zero()
        {
            Action action = () => new TimeBudget(Clock.Current, TimeSpan.Zero);

            action.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public async Task TimeBudget_IsExceeded_returnes_true_after_budget_duration_has_passed()
        {
            using (var clock = VirtualClock.Start())
            {
                var budget = new TimeBudget(clock, 5.Seconds());

                await clock.AdvanceBy(6.Seconds());

                budget.IsExceeded.Should().BeTrue();
            }
        }

        [Fact]
        public async Task TimeBudget_IsExceeded_returnes_false_before_budget_duration_has_passed()
        {
            using (var clock = VirtualClock.Start())
            {
                var budget = new TimeBudget(clock, 5.Seconds());

                await clock.AdvanceBy(2.Seconds());

                budget.IsExceeded.Should().BeFalse();
            }
        }

        [Fact]
        public async Task TimeBudget_can_throw_if_no_time_is_left()
        {
            var startTime = DateTimeOffset.Parse(
                "2017-01-01 12:00am +00:00",
                CultureInfo.InvariantCulture);

            using (var clock = VirtualClock.Start(startTime))
            {
                var budget = new TimeBudget(clock, 5.Seconds());

                await clock.AdvanceBy(1.Seconds());

                budget.Consume("one");

                await clock.AdvanceBy(10.Seconds());

                Action action = () => budget.ConsumeAndThrowIfBudgetExceeded("two");

                action.ShouldThrow<TimeBudgetExceededException>()
                      .Which
                      .Message
                      .Should()
                      .Be($"Time budget of 5 seconds exceeded at {clock.Now()}{NewLine}" +
                          $"  ✔ one @ 1 seconds{NewLine}" +
                          $"  ❌ two @ 11 seconds (budget exceeded by 6 seconds)");
            }
        } 

        [Fact]
        public async Task TimeBudget_can_be_used_to_mark_down_time_spent_in_an_operation()
        {
            using (var clock = VirtualClock.Start())
            {
                var budget = new TimeBudget(clock, 15.Seconds());

                await clock.AdvanceBy(5.Seconds());

                budget.Consume("one");

                await clock.AdvanceBy(8.Seconds());

                budget.Consume("two");

                await clock.AdvanceBy(13.Seconds());

                budget.Consume("three");

                budget.Entries
                      .Select(e => e.ToString())
                      .Should()
                      .BeEquivalentTo(
                          "✔ one @ 5 seconds",
                          "✔ two @ 13 seconds",
                          "❌ three @ 26 seconds (budget exceeded by 11 seconds)");
            }
        }

        [Fact]
        public async Task TimeBudget_exposes_a_CancellationToken_that_is_cancelled_when_the_budget_is_exceeded()
        {
            using (var clock = VirtualClock.Start())
            {
                var budget = new TimeBudget(clock, 10.Seconds());

                var token = budget.CancellationToken;

                await clock.AdvanceBy(5.Seconds());

                token.IsCancellationRequested.Should().BeFalse();

                await clock.AdvanceBy(5.Seconds());

                token.IsCancellationRequested.Should().BeTrue();
            }
        }
    }
}
