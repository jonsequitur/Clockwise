using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandReceiver<out T>
    {
        IDisposable Subscribe(
            CommandHandler<T> handle);

        Task<ICommandDeliveryResult> Receive(
            CommandHandler<T> handle,
            TimeSpan? timeout = null);
    }
}