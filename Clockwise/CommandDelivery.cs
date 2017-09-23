using System;

namespace Clockwise
{
    public static class CommandDelivery
    {
        public static CancelDeliveryResult<T> Cancel<T>(
            this ICommandDelivery<T> commandDelivery,
            string reason = null,
            Exception exception = null) =>
            new CancelDeliveryResult<T>(commandDelivery, reason, exception);

        public static CompleteDeliveryResult<T> Complete<T>(
            this ICommandDelivery<T> commandDelivery) =>
            new CompleteDeliveryResult<T>(commandDelivery);

        public static RetryDeliveryResult<T> Retry<T>(
            this ICommandDelivery<T> commandDelivery,
            TimeSpan? after = null) =>
            new RetryDeliveryResult<T>(
                commandDelivery, after ??
                                 Settings.For<T>
                                              .Default
                                              .RetryPolicy
                                              .RetryPeriodAfter(commandDelivery.NumberOfPreviousAttempts) ??
                                 throw new ArgumentException("No more retries available"));
    }
}
