using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Clockwise.Tests
{
    public class TaskExtensionsTests
    {
        [Fact]
        public void CancelAfter_can_use_a_CancellationToken_to_cancel_a_non_cancellable_task()
        {
            var source = new CancellationTokenSource();

            Func<Task> x = async () => await Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);
                    source.Cancel();
                }
            }).CancelAfter(source.Token);

            x.Should().Throw<TimeoutException>();
        }

        [Fact]
        public async Task CancelAfter_can_perform_an_action_rather_than_throw_if_cancellation_occurs()
        {
            var actionPerformed = false;
            var source = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);
                    source.Cancel();
                }
            }).CancelAfter(source.Token,
                           ifCancelled: () => actionPerformed = true);

            actionPerformed.Should().BeTrue();
        }

        [Fact]
        public void CancelAfter_T_can_use_a_CancellationToken_to_cancel_a_non_cancellable_task()
        {
            var source = new CancellationTokenSource();

            Func<Task<bool>> x = async () => await Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);
                    source.Cancel();
                }

                return true;
            }).CancelAfter(source.Token);

            x.Should().Throw<TimeoutException>();
        }

        [Fact]
        public async Task CancelAfter_T_returns_the_value_from_a_task_that_completes_before_cancellation()
        {
            var source = new CancellationTokenSource(10.Seconds());

            var result = await Task.Run(() => true)
                                   .CancelAfter(source.Token);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CancelAfter_T_can_return_a_fallback_value_rather_than_throw_if_cancellation_occurs()
        {
            var source = new CancellationTokenSource();

            var result = await Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);
                    source.Cancel();
                }

                return "not cancelled";
            }).CancelAfter(source.Token,
                           ifCancelled: () => "cancelled");

            result.Should().Be("cancelled");
        }
    }
}
