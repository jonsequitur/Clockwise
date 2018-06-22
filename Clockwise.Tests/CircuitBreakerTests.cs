using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Clockwise.Tests
{
    public class CircuitBreakerTests
    {
        [Fact]
        public void CircuitBreaker_can_be_use_to_intercept_delivery()
        {
            var processed = new List<int>();
            var cb = new CircuitBraker(new InMemoryCircuitBreakerStorage());
            var handler = CommandHandler.Create<int>(delivery =>
                {
                    if (delivery.Command > 10)
                    {
                        cb.OnFailure();
                    }
                    else
                    {
                        processed.Add(delivery.Command);
                    }
                })
                .UseMiddleware(async (delivery, next) =>
                {
                    if (cb.State == CircuitBreakerState.Closed)
                    {
                        return await next(delivery);
                    }

                    return null;
                });

            handler.Handle(new CommandDelivery<int>(1));
            handler.Handle(new CommandDelivery<int>(2));
            handler.Handle(new CommandDelivery<int>(11));
            handler.Handle(new CommandDelivery<int>(3));

            processed.Should().BeEquivalentTo(1, 2);

            cb.Close();
            handler.Handle(new CommandDelivery<int>(3));

            processed.Should().BeEquivalentTo(1, 2, 3);
        }
    }
}