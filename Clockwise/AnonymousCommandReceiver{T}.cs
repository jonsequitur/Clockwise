using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandReceiver<T> : ICommandReceiver<T>
    {
        private readonly Func<Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>, TimeSpan?, Task<ICommandDeliveryResult>> receive;

        private readonly Func<Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>, IDisposable> subscribe;

        public AnonymousCommandReceiver(
            Func<Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>, TimeSpan?, Task<ICommandDeliveryResult>> receive,
            Func<Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>, IDisposable> subscribe)
        {
            this.subscribe = subscribe ??
                             throw new ArgumentNullException(nameof(subscribe));

            this.receive = receive ??
                           throw new ArgumentNullException(nameof(receive));
        }

        public IDisposable Subscribe(Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> onNext)
        {
            return subscribe(onNext);
        }

        public async Task<ICommandDeliveryResult> Receive(Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> handle, TimeSpan? timeout = null)
        {
            return await receive(handle, timeout);
        }
    }
}
