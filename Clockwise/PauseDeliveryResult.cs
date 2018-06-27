using System;

namespace Clockwise
{
    public class PauseDeliveryResult<T> : CommandDeliveryResult<T>
    {
        public TimeSpan Duration { get; }

        public PauseDeliveryResult(ICommandDelivery<T> delivery, TimeSpan duration) : base(delivery)
        {
            Duration = duration;
        }
    }
}
