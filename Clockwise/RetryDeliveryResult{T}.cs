using System;

namespace Clockwise
{
    public class RetryDeliveryResult<T> : CommandDeliveryResult<T>
    {
        public RetryDeliveryResult(
            ICommandDelivery<T> delivery,
            TimeSpan retryPeriod) : base(delivery)
        {
            RetryPeriod = retryPeriod;

            if (delivery is CommandDelivery<T> d)
            {
                d.SignalRetry(RetryPeriod);
            }
        }

        public TimeSpan RetryPeriod { get; }

        public Exception Exception { get; private set; }

        public void SetException(Exception exception) => Exception = exception;
    }
}
