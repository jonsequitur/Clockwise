using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public class VirtualClock : IClock, IDisposable
    {
        private static readonly Logger logger = Logger<VirtualClock>.Log;

        private readonly SortedList<DateTimeOffset, Action<VirtualClock>> schedule = new SortedList<DateTimeOffset, Action<VirtualClock>>();

        private readonly string createdBy;

        private DateTimeOffset now;

        private VirtualClock(DateTimeOffset? now = null, [CallerMemberName] string createdBy = null)
        {
            this.createdBy = createdBy;
            this.now = now ?? DateTimeOffset.UtcNow;
            Clock.Current = this;
        }

        public void Dispose() => Clock.Reset();

        public static VirtualClock Start(
            DateTimeOffset? now = null)
        {
            if (Clock.Current is VirtualClock)
            {
                throw new InvalidOperationException("A virtual clock cannot be started while another is still active in the current context.");
            }

            var virtualClock = new VirtualClock(now);

            logger.Trace("Starting at {now}", now);

            return virtualClock;
        }

        public DateTimeOffset Now() => now;

        public async Task AdvanceTo(DateTimeOffset time)
        {
            await Task.Yield();

            if (time <= now)
            {
                throw new ArgumentException("The clock cannot be moved backward in time.");
            }

            using (var operation = AndConfirmAdvancement(now, time))
            {
                while (schedule.Count > 0)
                {
                    var next = schedule.Keys[0];

                    if (next > time)
                    {
                        break;
                    }

                    now = next;
                    var scheduleValue = schedule.Values[0];
                    schedule.RemoveAt(0);
                    scheduleValue.Invoke(this);
                }

                operation.Succeed();

                now = time;
            }
        }

        public async Task AdvanceBy(TimeSpan timespan) =>
            await AdvanceTo(now.Add(timespan));

        public override string ToString() => $"{now} [created by {createdBy}]";

        public void Schedule(
            Action<IClock> action,
            DateTimeOffset? after = null)
        {
            DateTimeOffset scheduledTime;

            if (after == null || after <= now)
            {
                scheduledTime = now.AddTicks(1);
            }
            else
            {
                scheduledTime = after.Value;
            }

            while (schedule.ContainsKey(scheduledTime))
            {
                scheduledTime = scheduledTime.AddTicks(1);
            }

            schedule.Add(scheduledTime, action);
        }

        public void Schedule(
            Func<IClock, Task> action,
            DateTimeOffset? after = null) =>
            Schedule(
                s => Task.Run(() => action(s)).Wait(),
                after);

        public TimeSpan? TimeUntilNextActionIsDue
        {
            get
            {
                for (var i = 0; i < schedule.Count; i++)
                {
                    var due = schedule.Keys[i];

                    if (due >= now)
                    {
                        return due - now;
                    }
                }

                return new TimeSpan?();
            }
        }

        private ConfirmationLogger AndConfirmAdvancement(DateTimeOffset start, DateTimeOffset end) =>
            new ConfirmationLogger(
                nameof(AdvanceTo),
                logger.Category,
                "Advancing from {start} ({startTicks}) to {end} ({endTicks})",
                args: new object[] { start, start.Ticks, end, end.Ticks },
                exitArgs: () => new[] { ("nowAt", (object) now) },
                logOnStart: true);
    }
}
