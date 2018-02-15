using System;
using System.Globalization;
using FluentAssertions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Environment;

namespace Clockwise.Tests
{
    public class CancellationTokenBudgetTests : IDisposable
    {
        protected void StartClock(DateTimeOffset? now = null) => VirtualClock.Start(now);

        public void Dispose() => Clock.Reset();

        [Fact]
        public void When_the_budget_is_created_then_the_start_time_is_captured()
        {
            StartClock();

            var budget = new TimeBudget(5.Seconds());

            budget.StartTime.Should().Be(Clock.Now());
        }

        [Fact]
        public async Task CancellationTokenBudget_throws_an_informative_exception_after_it_is_cancelled()
        {
            StartClock();

            var budget = new CancellationTokenBudget();
            
            await Clock.Current.Wait(1.Seconds());

            budget.RecordEntry("one");

            await Clock.Current.Wait(10.Seconds());

            budget.Cancel();

            Action action = () => budget.RecordEntryAndThrowIfBudgetExceeded("two");

            action.ShouldThrow<BudgetExceededException>()
                  .Which
                  .Message
                  .Should()
                  .Be($"Budget exceeded.{NewLine}" +
                      $"  ✔ one @ 1.00 seconds{NewLine}" +
                      $"  ❌ two @ 11.00 seconds");
        }

        [Fact]
        public async Task CancellationTokenBudget_does_not_expire_due_to_the_passage_of_time()
        {
            using (VirtualClock.Start())
            {
                var budget = new CancellationTokenBudget();

                await Clock.Current.Wait(1.Minutes());

                budget.IsExceeded.Should().BeFalse();
                budget.ElapsedDuration.Should().Be(1.Minutes());
            }
        }

        [Fact]
        public void CancellationTokenBudget_works_with_realtime_clock()
        {
            var budget = new CancellationTokenBudget();

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
        public void CancellationTokenBudget_ISExceeded_is_based_on_source_token_cancellation_state()
        {
            var cts = new CancellationTokenSource();

            var budget = new CancellationTokenBudget(cts.Token);

            budget.IsExceeded.Should().BeFalse();

            cts.Cancel();

            budget.IsExceeded.Should().BeTrue();
        }
    }
}
