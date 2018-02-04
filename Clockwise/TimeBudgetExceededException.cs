using System;
using System.Linq;
using static System.Environment;

namespace Clockwise
{
    public class TimeBudgetExceededException : Exception
    {
        public TimeBudgetExceededException(TimeBudget budget) : base(BuildMessage(budget))
        {
        }

        private static string BuildMessage(TimeBudget budget)
        {
            var now = budget.Clock.Now();

            var ws =
                budget.Entries.Any()
                    ? $"{NewLine}  {string.Join($"{NewLine}  ", budget.Entries.OrderBy(w => w.ElapsedDuration).Select(c => c.ToString()))}"
                    : "";

            return $"Time budget of {budget.TotalDuration.TotalSeconds} seconds exceeded at {now}{ws}";
        }
    }
}
