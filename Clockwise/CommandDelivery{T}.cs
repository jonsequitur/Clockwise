using System;
using System.Collections.Generic;

namespace Clockwise
{
    public class CommandDelivery<T> : ICommandDelivery<T>
    {
        public CommandDelivery(
            T command,
            DateTimeOffset? dueTime = null,
            DateTimeOffset? originalDueTime = null,
            string idempotencyToken = null,
            int numberOfPreviousAttempts = 0)
        {
            Command = command;

            IdempotencyToken = idempotencyToken ??
                               DeliveryContext.Current?.NextToken("") ??
                               Guid.NewGuid().ToString().ToToken();

            DueTime = dueTime;

            OriginalDueTime = originalDueTime ??
                              DueTime ??
                              Clock.Now();

            NumberOfPreviousAttempts = numberOfPreviousAttempts;
        }

        public T Command { get; }

        public DateTimeOffset? DueTime { get; private set; }

        public DateTimeOffset? OriginalDueTime { get; }

        public int NumberOfPreviousAttempts { get; private set; }

        public string IdempotencyToken { get; }

        public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        internal void SignalRetry(TimeSpan? after = null)
        {
            NumberOfPreviousAttempts++;

            DueTime = (DueTime ?? Clock.Now()) + after;
        }
    }
}
