using System;
using System.Collections.Generic;

namespace Clockwise
{
    public interface ICommandDelivery : IIdempotent
    {
        DateTimeOffset? DueTime { get; }

        DateTimeOffset? OriginalDueTime { get; }

        int NumberOfPreviousAttempts { get; }

        IDictionary<string, object> Properties { get; }
    }
}
