using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clockwise
{
    public static class TaskExtensions
    {
        public static async Task<T> CancelAfter<T>(
            this Task<T> task,
            CancellationToken cancellationToken,
            Func<T> ifCancelled = null)
        {
            var timeout = Task.Delay(-1, cancellationToken);

            Task firstToComplete = await Task.WhenAny(
                task,
                timeout);

            if (firstToComplete == timeout)
            {
                if (ifCancelled == null)
                {
                    throw new TimeoutException();
                }
                else
                {
                    return ifCancelled();
                }
            }

            return await task;
        }

        public static async Task CancelAfter(
            this Task task,
            CancellationToken cancellationToken,
            Action ifCancelled = null)
        {
            var timeout = Task.Delay(-1, cancellationToken);

            var firstToComplete = await Task.WhenAny(
                                      task,
                                      timeout);

            if (firstToComplete == timeout)
            {
                if (ifCancelled == null)
                {
                    throw new TimeoutException();
                }
                else
                {
                    ifCancelled();
                }
            }
        }
    }
}
