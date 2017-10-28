using System;
using System.Collections.Generic;
using FluentAssertions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Clockwise.Tests
{
    public class DeliveryContextTests
    {
        [Fact]
        public async Task DeliveryContext_Current_differs_per_async_context()
        {
            var barrier = new Barrier(2);

            string commandInTask1 = null;

            string commandInTask2 = null;

            await Task.Run(() =>
            {
                using (DeliveryContext.Establish(
                    new CommandDelivery<string>("", idempotencyToken: "one")))
                {
                    barrier.SignalAndWait(1000);
                    commandInTask1 = DeliveryContext.Current.Delivery.IdempotencyToken;
                }
            });

            await Task.Run(() =>
            {
                using (DeliveryContext.Establish(
                    new CommandDelivery<string>("", idempotencyToken: "two")))
                {
                    barrier.SignalAndWait(1000);
                    commandInTask2 = DeliveryContext.Current.Delivery.IdempotencyToken;
                }
            });

            commandInTask1.Should().Be("one");
            commandInTask2.Should().Be("two");
        }

        [Fact]
        public void A_series_of_calls_to_NextToken_produces_a_different_token_per_call()
        {
            var token = Guid.NewGuid().ToString();

            var tokens = new List<string>();

            using (var ctx = DeliveryContext.Establish(new CommandDelivery<string>("hi!")))
            {
                Enumerable.Range(1, 100).ToList().ForEach(_ => tokens.Add(ctx.NextToken(token)));
            }

            tokens.Distinct().Should().HaveCount(100);
        }

        [Fact]
        public void A_series_of_calls_to_NextToken_produces_the_same_sequence_given_the_same_source_token()
        {
            var sequenceToken = Guid.NewGuid().ToString();
            var idempotencyToken = Guid.NewGuid().ToString();

            var sequence1 = new List<string>();
            var sequence2 = new List<string>();

            using (var ctx = DeliveryContext.Establish(new CommandDelivery<string>("hi!", idempotencyToken: idempotencyToken)))
            {
                Enumerable.Range(1, 10).ToList().ForEach(_ => sequence1.Add(ctx.NextToken(sequenceToken)));
            }

            using (var ctx = DeliveryContext.Establish(new CommandDelivery<string>("hi!", idempotencyToken: idempotencyToken)))
            {
                Enumerable.Range(1, 10).ToList().ForEach(_ => sequence2.Add(ctx.NextToken(sequenceToken)));
            }

            sequence1.Should().Equal(sequence2);
        }

        [Fact]
        public void Different_source_tokens_produce_different_next_tokens()
        {
            var token1 = Guid.NewGuid().ToString();
            var token2 = Guid.NewGuid().ToString();
            var sequence1 = new List<string>();
            var sequence2 = new List<string>();

            var command = new CommandDelivery<string>("");

            using (var ctx = DeliveryContext.Establish(command))
            {
                Enumerable.Range(1, 10).ToList().ForEach(_ => sequence1.Add(ctx.NextToken(token1)));
            }

            using (var ctx = DeliveryContext.Establish(command))
            {
                Enumerable.Range(1, 10).ToList().ForEach(_ => sequence2.Add(ctx.NextToken(token2)));
            }

            sequence1.Should().NotIntersectWith(sequence2);
        }

        [Fact]
        public void Token_algorithm_should_not_change()
        {
            // if this test fails then it indicates that the sequential etag algorithm has changed
            var etagSequenceProducedByPreviousVersion = new[]
            {
                "7sNkA6fd+WqICHfZpSpca2bETG9e3AV0gwZP5/6zkVA=", "KJYYEMLms/h7kNhd8bmrrEhuGWFYAyMCr9UGtPR9kTY=",
                "SWS6CjQM9taGbF3eYnG6xYrd5gkQyKa9b5KJg3o2YfQ=", "LtN9an7jOaKR3LShgocAH2edLzj8FGTuQ8z9wvMFzjg=",
                "Yl/xj5eSYvywL8basZi0hsBYLWmAki6BG9KfbnpuYuc=", "qUEIZJOFwsI29ISwn25y2kstLOx//NSfVc76hY+KsEM=",
                "2UDKYnVH6sBfnEn0mAXMb1clOayxhqA4wjLoXRYhuV0=", "vzQI9iIC6sQqD/Dk0D3ugVyjrvDeM7FoH+v2Y9H/emA=",
                "iCqOdnu3UnIN68fVQyqZd9AD/PpW8+Qc2fkGT+9PUkc=", "EkHe5MMDkZkJiapyKEfSbKG6XuWhhZZLtD2UUlUML54="
            };

            var newlyGeneratedSequence = new List<string>();

            var token = "some specific token";

            using (var ctx = DeliveryContext.Establish(new CommandDelivery<string>("hello", idempotencyToken: "some specific value")))
            {
                Enumerable.Range(1, 10).ToList().ForEach(_ => newlyGeneratedSequence.Add(ctx.NextToken(token)));
            }

            newlyGeneratedSequence
                .Should()
                .BeEquivalentTo(etagSequenceProducedByPreviousVersion);
        }

        [Fact]
        public void Nested_command_contexts_maintain_independent_token_sequences()
        {
            var sourceToken = Guid.NewGuid().ToString();
            var outerCommand = new CommandDelivery<string>("");

            var sequenceWithoutInnerContext = new List<string>();
            var sequenceWithInnerContext = new List<string>();

            using (var context = DeliveryContext.Establish(outerCommand))
            {
                sequenceWithoutInnerContext.Add(context.NextToken(sourceToken));
                sequenceWithoutInnerContext.Add(context.NextToken(sourceToken));
            }

            using (var outerContext = DeliveryContext.Establish(outerCommand))
            {
                sequenceWithInnerContext.Add(outerContext.NextToken(sourceToken));

                using (var innerCtx = DeliveryContext.Establish(
                    new CommandDelivery<object>(
                        new object(), idempotencyToken: Guid.NewGuid().ToString())))
                {
                    innerCtx.NextToken(Guid.NewGuid().ToString());
                }

                sequenceWithInnerContext.Add(outerContext.NextToken(sourceToken));
            }

            sequenceWithInnerContext.Should().Equal(sequenceWithoutInnerContext);
        }
    }
}
