using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clockwise
{
    public static class TaskExtensions
    {
        public static Task<T> CancelAfter<T>(
            this Task<T> task,
            CancellationToken cancellationToken,
            Func<T> ifCancelled = null)
        {
            if (task.IsCompleted)
            {
                return task;
            }

            var timeout = Task.Delay(-1, cancellationToken);

            return Task.Run(async () =>
            {
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
                        return ifCancelled();
                    }
                }

                return task.Result;
            });
        }

        public static Task CancelAfter(
            this Task task,
            CancellationToken cancellationToken,
            Action ifCancelled = null)
        {
            if (task.IsCompleted)
            {
                return task;
            }

            var timeout = Task.Delay(-1, cancellationToken);

            return Task.Run(async () =>
            {
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
            });
        }
    }
}
