using System;

namespace Clockwise
{
    public class RetryDeliveryResult<T> : CommandDeliveryResult<T>
    {
        public RetryDeliveryResult(
            CommandDelivery<T> commandDelivery,
            TimeSpan retryPeriod) : base(commandDelivery)
        {
            RetryPeriod = retryPeriod;

            commandDelivery.SignalRetry(RetryPeriod);
        }

        public TimeSpan RetryPeriod { get; }

        public Exception Exception { get; private set; }

        public void SetException(Exception exception) => Exception = exception;
    }
}
