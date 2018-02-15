using Xunit.Abstractions;

namespace Clockwise.Tests
{
    public class RealtimeClockBudgetExtensionsTests : BudgetExtensionsTests
    {
        public RealtimeClockBudgetExtensionsTests( ITestOutputHelper output) :
            base(new RealtimeClock(), output)
        {
        }
    }
}