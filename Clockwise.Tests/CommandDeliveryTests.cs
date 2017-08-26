using System;
using FluentAssertions;
using Xunit;

namespace Clockwise.Tests
{
    public class CommandDeliveryTests
    {
        [Fact]
        public void Deliveries_have_a_default_idempotency_token()
        {
            var delivery = new CommandDelivery<string>("hi");

            delivery.IdempotencyToken.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void When_dates_are_not_specified_then_original_due_time_is_set_to_Clock_Now()
        {
            using (VirtualClock.Start())
            {
                new CommandDelivery<string>("").OriginalDueTime.Should().Be(Clock.Now());
            }
        }

        [Fact]
        public void Idempotency_tokens_are_generated_by_default_based_on_the_delivery_context_idempotency_token()
        {
            var delivery = new CommandDelivery<string>("hi",
                                                       idempotencyToken: "the-original-idempotency-token");

            string token1, token2;

            using (DeliveryContext.Establish(delivery))
            {
                token1 = new CommandDelivery<string>("").IdempotencyToken;
            }

            using (DeliveryContext.Establish(delivery))
            {
                token2 = new CommandDelivery<string>("").IdempotencyToken;
            }

            token2.Should().Be(token1);
        }
    }
}
