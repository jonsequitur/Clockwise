using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using static System.Environment;

namespace Clockwise
{
    public class TimeBudget
    {
        private readonly IClock clock;

        private readonly ConcurrentBag<TimeBudgetEntry> entries = new ConcurrentBag<TimeBudgetEntry>();

        public TimeBudget(IClock clock, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentException($"{nameof(duration)} must be greater than zero.");
            }

            TotalDuration = duration;

            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

            StartTime = clock.Now();

            CancellationToken = clock.CreateCancellationToken(duration);
        }

        public DateTimeOffset StartTime { get; }

        public TimeSpan RemainingDuration => TotalDuration - ElapsedDuration;

        public TimeSpan ElapsedDuration => clock.Now() - StartTime;

        public TimeSpan TotalDuration { get; }

        public bool IsExceeded => RemainingDuration < TimeSpan.Zero;

        public IReadOnlyCollection<TimeBudgetEntry> Entries => entries.OrderBy(e => e.ElapsedDuration).ToArray();

        public CancellationToken CancellationToken { get; }

        public void RecordEntry([CallerMemberName] string name = null)
        {
            entries.Add(new TimeBudgetEntry(name, this));
        }

        public void RecordEntryAndThrowIfBudgetExceeded([CallerMemberName] string name = null)
        {
            var now = clock.Now();

            RecordEntry(name);

            if (IsExceeded)
            {
                var ws =
                    entries.Any()
                        ? $"{NewLine}  {string.Join($"{NewLine}  ", entries.OrderBy(w => w.ElapsedDuration).Select(c => c.ToString()))}"
                        : "";

                throw new TimeBudgetExceededException($"Time budget of {TotalDuration.TotalSeconds} seconds exceeded at {now}{ws}");
            }
        }
    }
}
