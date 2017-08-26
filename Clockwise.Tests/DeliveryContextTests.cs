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
                "dM0F0CTmKQpAngK9tYgq/A==", "NxE5yC5V7I8BMBrUPMHKeQ==", "eN1q1+7HXXdDYow/qU80Fg==", "bbyUFL6Hee1cLancyW+iSQ==", "uITytyOZwtA6N0lMI0a1Kw==",
                "WJ/wpF4bz2/vpTyHwLu5rg==", "RpXqssQ3o0vZSevaUfFJcA==", "eJxBtnoGVd/j+5JU0Qv/tA==", "Ry8jn92BPj3u/vUCDL8bXA==", "43pnV6fNpbBkBHay9wNPoQ=="
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
