using System;

namespace Clockwise
{
    public abstract class CommandDeliveryResult<T>
    {
        protected CommandDeliveryResult(ICommandDelivery<T> delivery)
        {
            Delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        }

        public ICommandDelivery<T> Delivery { get; }
    }
}
