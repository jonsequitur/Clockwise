using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandReceiver<out T>
    {
        IDisposable Subscribe(
            HandleCommand<T> handle);

        Task<ICommandDeliveryResult> Receive(
            HandleCommand<T> handle,
            TimeSpan? timeout = null);
    }
}