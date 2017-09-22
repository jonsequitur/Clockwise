using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandScheduler<in T>
    {
        Task Schedule(ICommandDelivery<T> delivery);
    }
}
