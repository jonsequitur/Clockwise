using System;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public static class ClockExtensions
    {
        public static void Schedule(
            this IClock clock,
            Action<IClock> action,
            TimeSpan? dueAfter) =>
            clock.Schedule(action, clock.Now() + dueAfter);

        public static void Schedule(
            this IClock clock,
            Func<IClock, Task> action,
            TimeSpan? dueAfter) =>
            clock.Schedule(action, clock.Now() + dueAfter);

        public static IDisposable Repeat(
            this IClock clock,
            Func<IClock, Task> action,
            Func<TimeSpan> interval)
        {
            var signal = new SignalDisposable();

            clock.Repeat(
                action,
                interval, 
                signal);

            return signal;
        }

        private static void Repeat(
            this IClock clock,
            Func<IClock, Task> action,
            Func<TimeSpan> interval,
            SignalDisposable signalDisposable) =>
            clock.Schedule(
                async c =>
                {
                    if (!signalDisposable.IsDisposed)
                    {
                        await action(c);

                        c.Repeat(action, interval, signalDisposable);
                    }
                }, interval());

        public static async Task Wait(
            this IClock clock,
            TimeSpan timespan)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            using (new OperationLogger(message: $"Waiting {timespan}", category: "Clock", logOnStart: true))
            {
                switch (clock)
                {
                    case VirtualClock c:
                        await c.AdvanceBy(timespan);
                        break;
                    default:
                        await Task.Delay(timespan);
                        break;
                }
            }
        }

        private class SignalDisposable : IDisposable
        {
            public void Dispose()
            {
                IsDisposed = true;
            }

            public bool IsDisposed { get; private set; }
        }
    }
}
