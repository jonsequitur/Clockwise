using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandReceiver<T> : ICommandReceiver<T>
    {
        private readonly Func<CommandHandler<T>, TimeSpan?, Task<ICommandDeliveryResult>> receive;

        private readonly Func<CommandHandler<T>, IDisposable> subscribe;

        public AnonymousCommandReceiver(
            Func<CommandHandler<T>, TimeSpan?, Task<ICommandDeliveryResult>> receive,
            Func<CommandHandler<T>, IDisposable> subscribe)
        {
            this.subscribe = subscribe ??
                             throw new ArgumentNullException(nameof(subscribe));

            this.receive = receive ??
                           throw new ArgumentNullException(nameof(receive));
        }

        public IDisposable Subscribe(CommandHandler<T> handle)
        {
            return subscribe(handle);
        }

        public async Task<ICommandDeliveryResult> Receive(CommandHandler<T> handle, TimeSpan? timeout = null)
        {
            return await receive(handle, timeout);
        }
    }
}
