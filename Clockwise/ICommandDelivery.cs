using System;

namespace Clockwise
{
    public interface ICommandDelivery
    {
        DateTimeOffset? OriginalDueTime { get; }

        int NumberOfPreviousAttempts { get; }

        string IdempotencyToken { get; }
    }
}