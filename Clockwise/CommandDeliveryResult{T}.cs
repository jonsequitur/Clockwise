using System;

namespace Clockwise
{
    public abstract class CommandDeliveryResult<T> : ICommandDeliveryResult
    {
        protected CommandDeliveryResult(CommandDelivery<T> delivery)
        {
            Delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        }

        public CommandDelivery<T> Delivery { get; }
    }
}