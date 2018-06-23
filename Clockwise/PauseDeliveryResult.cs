using System;

namespace Clockwise
{
    public class PauseDeliveryResult<T> : CommandDeliveryResult<T>
    {
        public TimeSpan PausePeriod { get; }

        public PauseDeliveryResult(ICommandDelivery<T> delivery, TimeSpan pausePeriod) : base(delivery)
        {
            PausePeriod = pausePeriod;
        }
    }
}