using Xunit.Abstractions;

namespace Clockwise.Tests
{
    public class RealtimeClockTimeBudgetExtensionsTests : TimeBudgetExtensionsTests
    {
        public RealtimeClockTimeBudgetExtensionsTests( ITestOutputHelper output) :
            base(new RealtimeClock(), output)
        {
        }
    }
}