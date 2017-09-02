using System;

namespace Clockwise
{
    public class CancelDeliveryResult<T> : CommandDeliveryResult<T>
    {
        public CancelDeliveryResult(
            ICommandDelivery<T> commandDelivery,
            string reason = null,
            Exception exception = null) : base(commandDelivery)
        {
            Reason = reason;
            Exception = exception;
        }

        public string Reason { get; }

        public Exception Exception { get; }
    }
}
