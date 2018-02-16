using System;

namespace Clockwise
{
    public class BudgetEntry
    {
        private readonly Budget budget;

        internal BudgetEntry(
            string name,
            Budget budget)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            }

            this.budget = budget;

            Name = name;
            ElapsedDuration = budget.ElapsedDuration;
            BudgetWasExceeded = budget.IsExceeded;
        }

        public string Name { get; }

        public TimeSpan ElapsedDuration { get; }

        public bool BudgetWasExceeded { get; }

        public override string ToString()
        {
            var symbol = BudgetWasExceeded
                             ? "❌"
                             : "✔";

            string exceededMessage = null;

            if (BudgetWasExceeded)
            {
                if (budget is TimeBudget timeBudget)
                {
                    exceededMessage = $" (budget {timeBudget.DurationDescription} exceeded by {Math.Abs((ElapsedDuration - timeBudget.TotalDuration).TotalSeconds):F2} seconds.)";
                }
            }
            else
            {
                exceededMessage = string.Empty;
            }

            return $"{symbol} {Name} @ {ElapsedDuration.TotalSeconds:F2} seconds{exceededMessage}";
        }
    }
}
