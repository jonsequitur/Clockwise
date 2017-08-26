namespace Clockwise
{
    internal class ClockContext
    {
        public IClock Clock { get; set; } = RealtimeClock.Instance;
    }
}