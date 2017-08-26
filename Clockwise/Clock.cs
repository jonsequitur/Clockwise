using System;
using System.Threading;

namespace Clockwise
{
    public static class Clock
    {
        private static readonly AsyncLocal<ClockContext> current = new AsyncLocal<ClockContext>();

        public static DateTimeOffset Now() => Current.Now();

        public static IClock Current
        {
            get => Context().Clock;
            set => Context().Clock = value;
        }

        private static ClockContext Context()
        {
            if (current.Value != null)
            {
                return current.Value;
            }

            return current.Value = new ClockContext();
        }

        public static void Reset() =>
            Context().Clock = new RealtimeClock();
    }
}
