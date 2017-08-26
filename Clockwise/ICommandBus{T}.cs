using System;

namespace Clockwise
{
    public interface ICommandBus<T> :
        ICommandScheduler<T>,
        ICommandReceiver<T>
    {
    }
}
