using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandReceiver<T>
    {
        IDisposable Subscribe(
            Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>> onNext);

        Task<CommandDeliveryResult<T>> Receive(
            Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>> onNext,
            TimeSpan? timeout = null);
    }
}