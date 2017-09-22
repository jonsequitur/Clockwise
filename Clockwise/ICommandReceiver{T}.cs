using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandReceiver<out T>
    {
        IDisposable Subscribe(
            Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> onNext);

        Task<ICommandDeliveryResult> Receive(
            Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> onNext,
            TimeSpan? timeout = null);
    }
}