using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Pocket;
using static Pocket.Logger<Clockwise.VirtualClock>;

namespace Clockwise
{
    public class VirtualClock : IClock, IDisposable
    {
        private readonly ConcurrentDictionary<DateTimeOffset, Action<VirtualClock>> schedule = new ConcurrentDictionary<DateTimeOffset, Action<VirtualClock>>();

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

            Log.Trace("Starting at {now}", now);

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

            Log.Trace("Advancing to {time} ({ticks})", time, time.Ticks);

            while (true)
            {
                var due = DueBetween(now, time).ToArray();

                if (!due.Any())
                {
                    break;
                }

                now = due[0].Key;

                due[0].Value?.Invoke(this);
            }

            now = time;
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

            while (!schedule.TryAdd(
                       scheduledTime,
                       action))
            {
                scheduledTime = scheduledTime.AddTicks(1);
            }
        }

        public void Schedule(
            Func<IClock, Task> action,
            DateTimeOffset? after = null) =>
            Schedule(
                s => Task.Run(() => action(s)).Wait(),
                after);

        private IOrderedEnumerable<KeyValuePair<DateTimeOffset, Action<VirtualClock>>> DueBetween(
            DateTimeOffset startTime,
            DateTimeOffset endTime) =>
            schedule
                .Where(pair => pair.Key > startTime &&
                               pair.Key <= endTime)
                .OrderBy(pair => pair.Key);

        public TimeSpan? TimeUntilNextActionIsDue =>
            schedule
                .Select(pair => pair.Key - now)
                .Where(t => t > TimeSpan.Zero)
                .OrderBy(t => t)
                .Select(t => new TimeSpan?(t))
                .FirstOrDefault();
    }
}
