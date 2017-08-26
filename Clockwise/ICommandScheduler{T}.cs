using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandScheduler<T>
    {
        Task Schedule(CommandDelivery<T> delivery);
    }
}
