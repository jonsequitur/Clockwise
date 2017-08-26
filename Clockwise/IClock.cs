using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface IClock
    {
        DateTimeOffset Now();

        void Schedule(
            Action<IClock> action,
            DateTimeOffset? after = null);

        void Schedule(
            Func<IClock, Task> action,
            DateTimeOffset? after = null);
    }
}
