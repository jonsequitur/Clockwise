using System;
using FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static System.Environment;

namespace Clockwise.Tests
{
    public class TimeBudgetTests : IDisposable
    {
        public TimeBudgetTests()
        {
            StartClock();
        }

        protected void StartClock(DateTimeOffset? now = null) => VirtualClock.Start(now);

        public void Dispose() => Clock.Reset();

        [Fact]
        public void When_the_budget_is_created_then_the_start_time_is_captured()
        {
            var budget = new TimeBudget(5.Seconds());

            budget.StartTime.Should().Be(Clock.Now());
        }

        [Fact]
        public async Task The_remaining_duration_can_be_checked()
        {
            var budget = new TimeBudget(5.Seconds());

            await Clock.Current.Wait(3.Seconds());

            budget.RemainingDuration.Should().BeCloseTo(2.Seconds());
        }

        [Fact]
        public async Task When_the_clock_is_advanced_then_start_time_does_not_change()
        {
            var startTime = Clock.Now();

            var budget = new TimeBudget(5.Seconds());

            await Clock.Current.Wait(5.Minutes());

            budget.StartTime.Should().Be(startTime);
        }

        [Fact]
        public void TimeBudget_cannot_be_created_with_a_timespan_of_zero()
        {
            Action action = () => new TimeBudget(TimeSpan.Zero);

            action.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public async Task TimeBudget_IsExceeded_returnes_true_after_budget_duration_has_passed()
        {
            var budget = new TimeBudget(5.Seconds());

            await Clock.Current.Wait(6.Seconds());

            budget.IsExceeded.Should().BeTrue();
        }

        [Fact]
        public async Task TimeBudget_IsExceeded_returnes_false_before_budget_duration_has_passed()
        {
            var budget = new TimeBudget(5.Seconds());

            await Clock.Current.Wait(2.Seconds());

            budget.IsExceeded.Should().BeFalse();
        }

        [Fact]
        public async Task When_TimeBudget_is_exceeded_then_RemainingDuration_is_zero()
        {
            var budget = new TimeBudget(5.Seconds());

            await Clock.Current.Wait(20.Seconds());

            budget.RemainingDuration.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public async Task TimeBudget_throws_an_informative_exception_if_no_time_is_left()
        {
            var budget = new TimeBudget(5.Seconds());

            await Clock.Current.Wait(1.Seconds());

            budget.RecordEntry("one");

            await Clock.Current.Wait(10.Seconds());

            Action action = () => budget.RecordEntryAndThrowIfBudgetExceeded("two");

            action.ShouldThrow<BudgetExceededException>()
                  .Which
                  .Message
                  .Should()
                  .Be($"Budget of 5 seconds exceeded.{NewLine}" +
                      $"  ✔ one @ 1.00 seconds{NewLine}" +
                      $"  ❌ two @ 11.00 seconds (budget of 5 seconds exceeded by 6.00 seconds.)");
        }

        [Fact]
        public async Task TimeBudget_can_be_used_to_mark_down_time_spent_in_an_operation()
        {
            var budget = new TimeBudget(15.Seconds());

            await Clock.Current.Wait(5.Seconds());

            budget.RecordEntry("one");

            await Clock.Current.Wait(8.Seconds());

            budget.RecordEntry("two");

            await Clock.Current.Wait(13.Seconds());

            budget.RecordEntry("three");

            budget.Entries
                  .Select(e => e.ToString())
                  .Should()
                  .BeEquivalentTo(
                      "✔ one @ 5.00 seconds",
                      "✔ two @ 13.00 seconds",
                      "❌ three @ 26.00 seconds (budget of 15 seconds exceeded by 11.00 seconds.)");
        }

        [Fact]
        public async Task TimeBudget_exposes_a_CancellationToken_that_is_cancelled_when_the_budget_is_exceeded()
        {
            var budget = new TimeBudget(10.Seconds());

            var token = budget.CancellationToken;

            await Clock.Current.Wait(5.Seconds());

            token.IsCancellationRequested.Should().BeFalse();

            await Clock.Current.Wait(5.Seconds());

            token.IsCancellationRequested.Should().BeTrue();
        }

        [Fact]
        public void TimeBudget_allows_cancellation()
        {
            var budget = new TimeBudget(30.Seconds());

            budget.Cancel();

            budget.IsExceeded.Should().BeTrue();
        }

        [Fact]
        public async Task TimeBudget_ToString_describes_entries()
        {
            var budget = new TimeBudget(5.Seconds());

            await Clock.Current.Wait(1.Seconds());

            budget.RecordEntry("one");

            await Clock.Current.Wait(10.Seconds());

            budget.RecordEntry("two");

            budget.ToString()
                  .Should()
                  .Be($"TimeBudget: 5 seconds{NewLine}  ✔ one @ 1.00 seconds{NewLine}  ❌ two @ 11.00 seconds (budget of 5 seconds exceeded by 6.00 seconds.)");
        }

        [Fact]
        public async Task TimeBudget_ToString_truncates_durations_for_readability()
        {
            var budget = new TimeBudget(5.Seconds());

            await Clock.Current.Wait(TimeSpan.FromMilliseconds(1010.235));

            budget.RecordEntry("one");

            await Clock.Current.Wait(10.Seconds());
            await Clock.Current.Wait(TimeSpan.FromMilliseconds(1100.052));

            budget.RecordEntry("two");

            budget.ToString()
                  .Should()
                  .Be($"TimeBudget: 5 seconds{NewLine}  ✔ one @ 1.01 seconds{NewLine}  ❌ two @ 12.11 seconds (budget of 5 seconds exceeded by 7.11 seconds.)");
        }

        [Fact]
        public async Task TimeBudget_ToString_does_not_change_when_time_passes_but_no_new_entries_are_added()
        {
            var budget = new TimeBudget(2.Seconds());

            await Clock.Current.Wait(1.Seconds());

            budget.RecordEntry();

            var stringAt1Second = budget.ToString();

            await Clock.Current.Wait(2.Seconds());

            var stringAt1Minute = budget.ToString();

            stringAt1Minute.Should().Be(stringAt1Second);
        }
    }
}
