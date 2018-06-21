using System;
using FluentAssertions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using Xunit;
using static System.Environment;

namespace Clockwise.Tests
{
    public class BudgetTests : IDisposable
    {
        public BudgetTests()
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
        public async Task Budget_throws_an_informative_exception_after_it_is_cancelled()
        {
            var budget = new Budget();

            await Clock.Current.Wait(1.Seconds());

            budget.RecordEntry("one");

            await Clock.Current.Wait(10.Seconds());

            budget.Cancel();

            Action action = () => budget.RecordEntryAndThrowIfBudgetExceeded("two");

            action.Should().Throw<BudgetExceededException>()
                  .Which
                  .Message
                  .Should()
                  .Be($"Budget exceeded.{NewLine}" +
                      $"  ✔ one @ 1.00 seconds{NewLine}" +
                      $"  ❌ two @ 11.00 seconds");
        }

        [Fact]
        public async Task Budget_does_not_expire_due_to_the_passage_of_time()
        {
            var budget = new Budget();

            await Clock.Current.Wait(1.Minutes());

            budget.IsExceeded.Should().BeFalse();
            budget.ElapsedDuration.Should().Be(1.Minutes());
        }

        [Fact]
        public void Budget_works_with_realtime_clock()
        {
            var budget = new Budget();

            budget.IsExceeded
                  .Should()
                  .BeFalse();
            budget.ElapsedDuration
                  .Should()
                  .BeLessOrEqualTo(1.Seconds());
            budget.RecordEntry("one");
            budget.Entries
                  .Last()
                  .BudgetWasExceeded
                  .Should()
                  .BeFalse();
        }

        [Fact]
        public void Budget_IsExceeded_is_based_on_source_token_cancellation_state()
        {
            var cts = new CancellationTokenSource();

            var budget = new Budget(cts.Token);

            budget.IsExceeded.Should().BeFalse();

            cts.Cancel();

            budget.IsExceeded.Should().BeTrue();
        }
    }
}
