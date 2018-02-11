using System;
using System.Linq;

namespace Clockwise
{
    public class TimeBudgetExceededException : Exception
    {
        public TimeBudgetExceededException(TimeBudget budget) : base(BuildMessage(budget))
        {
        }

        private static string BuildMessage(TimeBudget budget) =>
            $"Time budget of {budget.TotalDuration.TotalSeconds} seconds exceeded at {budget.Clock.Now()}{budget.EntriesDescription}";
    }
}
