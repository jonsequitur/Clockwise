using System;

namespace Clockwise
{
    public class CircuitBreakerStateDescriptor
    {
        public CircuitBreakerState State { get; }
        public TimeSpan? TimeToLive { get; }
        public DateTimeOffset TimeStamp { get;  }

        public CircuitBreakerStateDescriptor(CircuitBreakerState state, DateTimeOffset timeStamp, TimeSpan? timeToLive = null)
        {
            State = state;
            TimeToLive = timeToLive;
            TimeStamp = timeStamp;
        }
    }
}