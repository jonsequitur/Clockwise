using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandReceiver<T> : ICommandReceiver<T>
    {
        private readonly Func<HandleCommand<T>, TimeSpan?, Task<ICommandDeliveryResult>> receive;

        private readonly Func<HandleCommand<T>, IDisposable> subscribe;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnonymousCommandReceiver{T}"/> class.
        /// </summary>
        /// <param name="receive">The receive.</param>
        /// <param name="subscribe">The subscribe.</param>
        /// <exception cref="ArgumentNullException">
        /// subscribe
        /// or
        /// receive
        /// </exception>
        public AnonymousCommandReceiver(
            Func<HandleCommand<T>, TimeSpan?, Task<ICommandDeliveryResult>> receive,
            Func<HandleCommand<T>, IDisposable> subscribe)
        {
            this.subscribe = subscribe ??
                             throw new ArgumentNullException(nameof(subscribe));

            this.receive = receive ??
                           throw new ArgumentNullException(nameof(receive));
        }

        public IDisposable Subscribe(HandleCommand<T> handle)
        {
            return subscribe(handle);
        }

        public async Task<ICommandDeliveryResult> Receive(HandleCommand<T> handle, TimeSpan? timeout = null)
        {
            return await receive(handle, timeout);
        }
    }
}
