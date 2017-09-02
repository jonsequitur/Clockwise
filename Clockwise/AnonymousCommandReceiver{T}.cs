using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandReceiver<T> : ICommandReceiver<T>
    {
        private readonly Func<Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>>, TimeSpan?, Task<CommandDeliveryResult<T>>> receive;

        private readonly Func<Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>>, IDisposable> subscribe;

        public AnonymousCommandReceiver(
            Func<Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>>, TimeSpan?, Task<CommandDeliveryResult<T>>> receive,
            Func<Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>>, IDisposable> subscribe)
        {
            this.subscribe = subscribe ??
                             throw new ArgumentNullException(nameof(subscribe));

            this.receive = receive ??
                           throw new ArgumentNullException(nameof(receive));
        }

        public IDisposable Subscribe(Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>> onNext)
        {
            return subscribe(onNext);
        }

        public async Task<CommandDeliveryResult<T>> Receive(Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle, TimeSpan? timeout = null)
        {
            return await receive(handle, timeout);
        }
    }
}
