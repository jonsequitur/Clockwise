using System;

namespace Clockwise
{
    public class TimeBudgetExceededException : Exception
    {
        public TimeBudgetExceededException(string message) : base(message)
        {
        }
    }
}