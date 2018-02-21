using System;

namespace Clockwise
{
    public class TimeBudget : Budget
    {
        private  TimeSpan? elapsedDurationAtCancellation;

        public TimeBudget(
            TimeSpan duration,
            IClock clock = null) : base(clock: clock)
        {
            if (duration == null ||
                duration <= TimeSpan.Zero)
            {
                throw new ArgumentException($"{nameof(duration)} must be greater than zero.");
            }

            TotalDuration = duration;

            CancellationTokenSource.CancelAfter(
                TotalDuration,
                Clock);

            CancellationToken.Register(() => elapsedDurationAtCancellation = TotalDuration -  CalculateRemainingDuration());
        }

        protected internal string DurationDescription =>
            $"of {TotalDuration.TotalSeconds} seconds";

        public TimeSpan RemainingDuration => CalculateRemainingDuration();

        public TimeSpan? ElapsedDurationAtCancellation => elapsedDurationAtCancellation;

        private TimeSpan CalculateRemainingDuration()
        {
            var remaining = TotalDuration - ElapsedDuration;

            return remaining < TimeSpan.Zero
                       ? TimeSpan.Zero
                       : remaining;
        }

        public TimeSpan TotalDuration { get; }

        public override string ToString() =>
            $"{nameof(TimeBudget)}: {TotalDuration.TotalSeconds} seconds{EntriesDescription}";
    }
}
