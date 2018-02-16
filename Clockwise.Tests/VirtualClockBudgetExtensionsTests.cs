using Xunit.Abstractions;

namespace Clockwise.Tests
{
    public class VirtualClockBudgetExtensionsTests : BudgetExtensionsTests
    {
        public VirtualClockBudgetExtensionsTests(ITestOutputHelper output) :
            base(VirtualClock.Start(), output)
        {
        }
    }
}