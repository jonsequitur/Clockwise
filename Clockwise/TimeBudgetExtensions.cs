using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public static class TimeBudgetExtensions
    {
        public static async Task CancelIfExceeds(
            this Task task,
            TimeBudget budget,
            Action ifCancelled = null)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            await task.CancelAfter(budget.CancellationToken,
                                   ifCancelled: () =>
                                   {
                                       if (ifCancelled == null)
                                       {
                                           throw new TimeBudgetExceededException(budget);
                                       }
                                       else
                                       {
                                           ifCancelled();
                                       }
                                   });
        }

        public static async Task<T> CancelIfExceeds<T>(
            this Task<T> task,
            TimeBudget budget,
            Func<T> ifCancelled = null)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            return await task.CancelAfter(
                       budget.CancellationToken,
                       ifCancelled: () => ifCancelled == null
                                              ? throw new TimeBudgetExceededException(budget)
                                              : ifCancelled());
        }
    }
}
