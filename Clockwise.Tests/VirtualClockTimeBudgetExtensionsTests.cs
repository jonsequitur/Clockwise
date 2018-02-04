using Xunit.Abstractions;

namespace Clockwise.Tests
{
    public class VirtualClockTimeBudgetExtensionsTests : TimeBudgetExtensionsTests
    {
        public VirtualClockTimeBudgetExtensionsTests(ITestOutputHelper output) :
            base(VirtualClock.Start(), output)
        {
        }
    }
}