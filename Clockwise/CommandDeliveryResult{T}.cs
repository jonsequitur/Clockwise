using System;

namespace Clockwise
{
    public class CommandDeliveryResult<T> : ICommandDeliveryResult
    {
        public CommandDeliveryResult(ICommandDelivery<T> delivery)
        {
            Delivery = delivery ??
                       throw new ArgumentNullException(nameof(delivery));
        }

        public ICommandDelivery<T> Delivery { get; }
    }
}
