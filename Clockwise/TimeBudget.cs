using System;

namespace Clockwise
{
    public class TimeBudget : Budget
    {
        public TimeBudget(
            TimeSpan duration,
            IClock clock = null) : base(clock)
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
        }

        protected internal override string DurationDescription =>
            $"of {TotalDuration.TotalSeconds} seconds";

        public TimeSpan RemainingDuration
        {
            get
            {
                var remaining = TotalDuration - ElapsedDuration;

                return remaining < TimeSpan.Zero
                           ? TimeSpan.Zero
                           : remaining;
            }
        }

        public TimeSpan TotalDuration { get; }

        public override string ToString() =>
            $"{nameof(TimeBudget)}: {TotalDuration.TotalSeconds} seconds{EntriesDescription}";
    }
}
