using System;
using System.Collections.Generic;

namespace Clockwise
{
    public interface ICommandDelivery
    {
        DateTimeOffset? DueTime { get; }

        DateTimeOffset? OriginalDueTime { get; }

        int NumberOfPreviousAttempts { get; }

        string IdempotencyToken { get; }

        IDictionary<string, object> Properties { get; }
    }
}
