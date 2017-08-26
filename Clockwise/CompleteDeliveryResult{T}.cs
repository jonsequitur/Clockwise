using System;

namespace Clockwise
{
    public class CompleteDeliveryResult<T> : CommandDeliveryResult<T>
    {
        public CompleteDeliveryResult(
            CommandDelivery<T> commandDelivery) : base(commandDelivery)
        {
        }
    }
}