using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Clockwise
{
    public class Budget
    {
        protected readonly ConcurrentBag<BudgetEntry> entries = new ConcurrentBag<BudgetEntry>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Budget"/> class.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="clock">The clock.</param>
        public Budget(
            CancellationToken? cancellationToken = null,
            IClock clock = null)
        {
            CancellationTokenSource = cancellationToken == null
                                          ? new CancellationTokenSource()
                                          : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value);

            Clock = clock ?? Clockwise.Clock.Current;

            StartTime = Clock.Now();
        }

        /// <summary>
        /// Gets the cancellation token.
        /// </summary>
        /// <value>
        /// The cancellation token.
        /// </value>
        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        /// <summary>
        /// Gets the cancellation token source.
        /// </summary>
        /// <value>
        /// The cancellation token source.
        /// </value>
        protected CancellationTokenSource CancellationTokenSource { get; }

        internal IClock Clock { get; }

        /// <summary>
        /// Gets the duration of the elapsed.
        /// </summary>
        /// <value>
        /// The duration of the elapsed.
        /// </value>
        public TimeSpan ElapsedDuration => Clock.Now() - StartTime;

        internal string EntriesDescription =>
            Entries.Any()
                ? $"{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", Entries.OrderBy(w => w.ElapsedDuration).Select(c => c.ToString()))}"
                : "";

        /// <summary>
        /// Gets a value indicating whether this budget is exceeded.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this budget is exceeded; otherwise, <c>false</c>.
        /// </value>
        public bool IsExceeded =>
            CancellationTokenSource.IsCancellationRequested;

        public IReadOnlyCollection<BudgetEntry> Entries => entries.OrderBy(e => e.ElapsedDuration).ToArray();

        /// <summary>
        /// Gets the start time.
        /// </summary>
        /// <value>
        /// The start time.
        /// </value>
        public DateTimeOffset StartTime { get; }

        /// <summary>
        /// Cancels this instance.
        /// </summary>
        public void Cancel() => CancellationTokenSource.Cancel();

        /// <summary>
        /// Records the entry.
        /// </summary>
        /// <param name="name">The name.</param>
        public void RecordEntry([CallerMemberName] string name = null) =>
            entries.Add(new BudgetEntry(name, this));

        /// <summary>
        /// Records the entry and throw if budget exceeded.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <exception cref="BudgetExceededException"></exception>
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
