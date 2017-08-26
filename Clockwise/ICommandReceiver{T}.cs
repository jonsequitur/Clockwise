using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandReceiver<T>
    {
        IDisposable Subscribe(
            Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> onNext);

        Task<CommandDeliveryResult<T>> Receive(
            Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> onNext,
            TimeSpan? timeout = null);
    }
}