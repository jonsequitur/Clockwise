﻿using System;
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

        public TimeBudget(TimeSpan duration, IClock clock = null) : this(false, duration, clock)
        {
        }

        private TimeBudget(
            bool unlimited,
            TimeSpan? duration = null,
            IClock clock = null)
        {
            Clock = clock ?? Clockwise.Clock.Current;

            StartTime = Clock.Now();

            if (unlimited)
            {
                TotalDuration = TimeSpan.MaxValue;
                CancellationToken = new CancellationTokenSource().Token;
            }
            else
            {
                if (duration == null ||
                    duration <= TimeSpan.Zero)
                {
                    throw new ArgumentException($"{nameof(duration)} must be greater than zero.");
                }

                TotalDuration = duration.Value;
                CancellationToken = Clock.CreateCancellationToken(TotalDuration);
            }
        }

        internal IClock Clock { get; }

        public DateTimeOffset StartTime { get; }

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

        public TimeSpan ElapsedDuration => Clock.Now() - StartTime;

        public TimeSpan TotalDuration { get; }

        public bool IsExceeded => RemainingDuration <= TimeSpan.Zero;

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

        public static TimeBudget Unlimited() => new TimeBudget(true);
    }
}
