using System;
using System.Globalization;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace Clockwise.Tests
{
    public class RetryDeliveryResultTests
    {
        [Fact]
        public void The_default_retry_backoff_is_exponential()
        {
            var retries = Enumerable.Range(0, 5)
                                    .Select(i =>
                                    {
                                        var delivery = new CommandDelivery<string>("hi", numberOfPreviousAttempts: i);

                                        return delivery.Retry();
                                    });

            retries
                .Select(a => a.RetryPeriod)
                .ShouldBeEquivalentTo(new[]
                {
                    1.Minutes(),
                    4.Minutes(),
                    9.Minutes(),
                    16.Minutes(),
                    25.Minutes()
                });
        }

        [Fact]
        public void When_a_retry_is_specified_then_the_due_time_is_calculated_from_the_original_due_time()
        {
            var originalDueTime = DateTimeOffset.Parse("2018-01-01 12:00pm +00:00", CultureInfo.InvariantCulture);

            var delivery = new CommandDelivery<string>(
                "hello",
                dueTime: originalDueTime);

            delivery.Retry(1.Days());

            delivery
                .DueTime
                .Should()
                .Be(originalDueTime + 1.Days());
        }

        [Fact]
        public void When_a_ret_is_specified_and_the_due_time_was_null_then_the_new_due_time_is_calculated_from_the_current_clock_time()
        {
            using (var clock = VirtualClock.Start())
            {
                var delivery = new CommandDelivery<string>(
                    "hello",
                    dueTime: null);

                delivery.Retry(1.Days());

                delivery
                    .DueTime
                    .Should()
                    .Be(clock.Now() + 1.Days());
            }
        }

        [Fact]
        public void When_a_retry_is_specified_then_the_original_due_time_is_unchanged()
        {
            var originalDueTime = DateTimeOffset.Parse("2018-01-01 12:00pm +00:00", CultureInfo.InvariantCulture);

            var delivery = new CommandDelivery<string>("hello", dueTime: originalDueTime);

            delivery.Retry(1.Days());

            delivery
                .OriginalDueTime
                .Should()
                .Be(originalDueTime);
        }
    }
}
