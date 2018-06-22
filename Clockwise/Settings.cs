using System;

namespace Clockwise
{
    public class Settings
    {
        public static class For<T>
        {
            public static Settings Default { get; set; } = new Settings();

            public static void Reset() => Default = new Settings();
        }

        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromHours(1);

        public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();
      
    }
}
