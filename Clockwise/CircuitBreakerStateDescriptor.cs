using System;

namespace Clockwise
{
    public class CircuitBreakerStateDescriptor : IEquatable<CircuitBreakerStateDescriptor>
    {
        public static bool operator ==(CircuitBreakerStateDescriptor left, CircuitBreakerStateDescriptor right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CircuitBreakerStateDescriptor left, CircuitBreakerStateDescriptor right)
        {
            return !Equals(left, right);
        }

        public bool Equals(CircuitBreakerStateDescriptor other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return State == other.State && TimeToLive.Equals(other.TimeToLive) && TimeStamp.Equals(other.TimeStamp);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var other = obj as CircuitBreakerStateDescriptor;
            return other != null && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) State;
                hashCode = (hashCode * 397) ^ TimeToLive.GetHashCode();
                hashCode = (hashCode * 397) ^ TimeStamp.GetHashCode();
                return hashCode;
            }
        }

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