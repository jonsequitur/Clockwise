using System;

namespace Clockwise
{
    public class CompleteDeliveryResult<T> : CommandDeliveryResult<T>
    {
        public CompleteDeliveryResult(
            ICommandDelivery<T> commandDelivery) : base(commandDelivery)
        {
        }
    }
}