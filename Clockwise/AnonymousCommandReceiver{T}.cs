using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandReceiver<T> : ICommandReceiver<T>
    {
        private readonly Func<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>, TimeSpan?, Task<CommandDeliveryResult<T>>> receive;

        private readonly Func<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>, IDisposable> subscribe;

        public AnonymousCommandReceiver(
            Func<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>, TimeSpan?, Task<CommandDeliveryResult<T>>> receive,
            Func<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>, IDisposable> subscribe)
        {
            this.subscribe = subscribe ??
                             throw new ArgumentNullException(nameof(subscribe));

            this.receive = receive ??
                           throw new ArgumentNullException(nameof(receive));
        }

        public IDisposable Subscribe(Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> onNext) => subscribe(onNext);

        public async Task<CommandDeliveryResult<T>> Receive(Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle, TimeSpan? timeout = null) =>
            await receive(handle, timeout);
    }
}
