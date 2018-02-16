using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Clockwise
{
    public abstract class Budget
    {
        protected readonly ConcurrentBag<BudgetEntry> entries = new ConcurrentBag<BudgetEntry>();

        protected Budget(
            IClock clock = null,
            CancellationToken? cancellationToken = null)
        {
            CancellationTokenSource = cancellationToken == null
                                          ? new CancellationTokenSource()
                                          : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value);

            Clock = clock ?? Clockwise.Clock.Current;

            StartTime = Clock.Now();
        }

        internal IClock Clock { get; }

        public TimeSpan ElapsedDuration => Clock.Now() - StartTime;

        public bool IsExceeded =>
            CancellationTokenSource.IsCancellationRequested;

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public IReadOnlyCollection<BudgetEntry> Entries => entries.OrderBy(e => e.ElapsedDuration).ToArray();

        public DateTimeOffset StartTime { get; }

        internal string EntriesDescription =>
            Entries.Any()
                ? $"{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", Entries.OrderBy(w => w.ElapsedDuration).Select(c => c.ToString()))}"
                : "";

        protected CancellationTokenSource CancellationTokenSource { get; }

        protected internal abstract string DurationDescription { get; }

        public void Cancel() => CancellationTokenSource.Cancel();

        public void RecordEntry([CallerMemberName] string name = null) =>
            entries.Add(new BudgetEntry(name, this));

        public void RecordEntryAndThrowIfBudgetExceeded([CallerMemberName] string name = null)
        {
            RecordEntry(name);

            if (IsExceeded)
            {
                throw new BudgetExceededException(this);
            }
        }
    }
}
