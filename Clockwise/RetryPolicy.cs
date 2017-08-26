using System;

namespace Clockwise
{
    public delegate TimeSpan? RetryDelegate(int numberOfPreviousAttempts);

    public class RetryPolicy
    {
        private readonly RetryDelegate shouldRetryAfter;

        public RetryPolicy(RetryDelegate shouldRetryAfter = null)
        {
            this.shouldRetryAfter = shouldRetryAfter ??
                                    Default;
        }

        public TimeSpan? RetryPeriodAfter(int numberOfPreviousAttempts) =>
            shouldRetryAfter(numberOfPreviousAttempts);

        private static TimeSpan? Default(int numberOfPreviousAttempts) =>
            TimeSpan.FromMinutes(
                Math.Pow(
                    numberOfPreviousAttempts + 1, 2));
    }
}
