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
            =>
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

        public static async Task<T> CancelIfExceeds<T>(
            this Task<T> task,
            TimeBudget budget,
            Func<T> ifCancelled = null)
            =>
                await task.CancelAfter(
                    budget.CancellationToken,
                    ifCancelled: () => ifCancelled == null
                                           ? throw new TimeBudgetExceededException(budget)
                                           : ifCancelled());
    }
}
