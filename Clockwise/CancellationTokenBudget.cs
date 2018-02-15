using System.Threading;

namespace Clockwise
{
    public class CancellationTokenBudget : Budget
    {
        public CancellationTokenBudget(CancellationToken?cancellationToken = null) : base(cancellationToken: cancellationToken)
        {
        }

        protected internal override string DurationDescription { get; }
    }
}
