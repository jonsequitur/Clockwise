using System;

namespace Clockwise
{
    public static class CommandDelivery
    {
        public static CancelDeliveryResult<T> Cancel<T>(
            this CommandDelivery<T> commandDelivery,
            string reason = null,
            Exception exception = null) =>
            new CancelDeliveryResult<T>(commandDelivery, reason, exception);

        public static CompleteDeliveryResult<T> Complete<T>(
            this CommandDelivery<T> commandDelivery) =>
            new CompleteDeliveryResult<T>(commandDelivery);

        public static RetryDeliveryResult<T> Retry<T>(
            this CommandDelivery<T> commandDelivery,
            TimeSpan? after = null) =>
            new RetryDeliveryResult<T>(
                commandDelivery, after ??
                                 Configuration.For<T>
                                              .Default
                                              .RetryPolicy
                                              .RetryPeriodAfter(commandDelivery.NumberOfPreviousAttempts) ??
                                 throw new ArgumentException("No more retries available"));
    }
}
