using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public class RealtimeClock : IClock
    {
        public DateTimeOffset Now() => DateTimeOffset.UtcNow;

        public void Schedule(
            Action<IClock> action,
            DateTimeOffset? after = null) =>
            Schedule(
                clock =>
                {
                    action(clock);
                    return Task.CompletedTask;
                },
                after);

        public void Schedule(
            Func<IClock, Task> action,
            DateTimeOffset? after = null)
        {
            var now = Now();

            if (after != null &&
                after.Value < now)
            {
                after = now;
            }

            var delay = after == null
                            ? TimeSpan.Zero
                            : after.Value - now;

            Task.Delay(delay)
                .ContinueWith(async _ => await action(this));
        }

        public static IClock Instance => new RealtimeClock();
    }
}
