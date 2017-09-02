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

            // FIX: (RetryDeliveryResult) this is ugly
            switch (delivery)
            {
                case CommandDelivery<T> d:
                    d.SignalRetry(RetryPeriod);
                    break;
            }
        }

        public TimeSpan RetryPeriod { get; }

        public Exception Exception { get; private set; }

        public void SetException(Exception exception) => Exception = exception;
    }
}
