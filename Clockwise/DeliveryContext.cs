using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Clockwise
{
    public class DeliveryContext : IDisposable
    {
        private static readonly AsyncLocal<Stack<DeliveryContext>> current = new AsyncLocal<Stack<DeliveryContext>>();

        private readonly ConcurrentDictionary<ICommandDelivery, TokenSequence> tokenSequences = new ConcurrentDictionary<ICommandDelivery, TokenSequence>();

        private readonly ICommandDelivery parentDelivery;

        private DeliveryContext(ICommandDelivery delivery)
        {
            Delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));

            if (current.Value?.Count > 0 &&
                current.Value?.Peek().Delivery != null)
            {
                parentDelivery = current.Value.Peek().Delivery;
            }
            else
            {
                parentDelivery = new NoParent(delivery.IdempotencyToken);
            }
        }

        public static DeliveryContext Establish(ICommandDelivery delivery)
        {
            var context = new DeliveryContext(delivery);

            if (current.Value == null)
            {
                current.Value = new Stack<DeliveryContext>();
            }

            current.Value.Push(context);

            return context;
        }

        public static DeliveryContext Current => current.Value?.Peek();

        public ICommandDelivery Delivery { get; }

        public string NextToken(string sourceToken)
        {
            if (sourceToken == null)
            {
                throw new ArgumentNullException(nameof(sourceToken));
            }

            var sequence = tokenSequences
                .GetOrAdd(parentDelivery, _ => new TokenSequence())
                .NextValue();

            var unhashedToken = $"{parentDelivery.IdempotencyToken}:{sourceToken} ({sequence})";

            return unhashedToken.ToToken();
        }

        public void Dispose() => current?.Value?.Pop();

        private class NoParent : ICommandDelivery
        {
            public NoParent(string idempotencyToken)
            {
                IdempotencyToken = idempotencyToken;
            }

            public string Id { get; }
            public DateTimeOffset? OriginalDueTime { get; }
            public int NumberOfPreviousAttempts { get; }
            public string IdempotencyToken { get; }
        }

        private class TokenSequence
        {
            private int value;

            public int NextValue() => Interlocked.Increment(ref value);
        }
    }
}
