using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Clockwise
{
    public class TimeBudget
    {
        private readonly ConcurrentBag<TimeBudgetEntry> entries = new ConcurrentBag<TimeBudgetEntry>();

        public TimeBudget(TimeSpan duration, IClock clock = null)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentException($"{nameof(duration)} must be greater than zero.");
            }

            TotalDuration = duration;

            Clock = clock ?? Clockwise.Clock.Current;

            StartTime = Clock.Now();

            CancellationToken = Clock.CreateCancellationToken(duration);
        }

        internal IClock Clock { get; }

        public DateTimeOffset StartTime { get; }

        public TimeSpan RemainingDuration => TotalDuration - ElapsedDuration;

        public TimeSpan ElapsedDuration => Clock.Now() - StartTime;

        public TimeSpan TotalDuration { get; }

        public bool IsExceeded => RemainingDuration < TimeSpan.Zero;

        public IReadOnlyCollection<TimeBudgetEntry> Entries => entries.OrderBy(e => e.ElapsedDuration).ToArray();

        public CancellationToken CancellationToken { get; }

        public void RecordEntry([CallerMemberName] string name = null) =>
            entries.Add(new TimeBudgetEntry(name, this));

        public void RecordEntryAndThrowIfBudgetExceeded([CallerMemberName] string name = null)
        {
            RecordEntry(name);

            if (IsExceeded)
            {
                throw new TimeBudgetExceededException(this);
            }
        }
    }
}
