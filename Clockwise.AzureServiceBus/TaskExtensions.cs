using System;
using System.Threading.Tasks;

namespace Clockwise.AzureServiceBus
{
    internal static class TaskExtensions
    {
        public static async Task<T> Timeout<T>(
            this Task<T> task,
            TimeSpan? timeout = null) =>
            task == await Task.WhenAny(
                task,
                Task.Delay(timeout ?? TimeSpan.FromMinutes(60)))
                ? await task
                : default(T);
    }
}
