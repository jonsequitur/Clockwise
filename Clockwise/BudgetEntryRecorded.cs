namespace Clockwise
{
    public delegate void BudgetEntryRecorded(
        VirtualClock clock,
        Budget budget,
        BudgetEntry entry);
}