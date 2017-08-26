using System;

namespace Clockwise
{
    public class Configuration
    {
        public static class For<T>
        {
            public static Configuration Default { get; set; } = new Configuration();

            public static void Reset() => Default = new Configuration();
        }

        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromHours(1);

        public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();
    }
}