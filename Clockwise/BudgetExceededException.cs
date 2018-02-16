using System;

namespace Clockwise
{
    public class BudgetExceededException : Exception
    {
        public BudgetExceededException(Budget budget) : base(BuildMessage(budget))
        {
        }

        private static string BuildMessage(Budget budget) =>
            $"Budget {budget.DurationDescription} exceeded.".Replace("  ", " ") + $"{budget.EntriesDescription}";
    }
}
