using System;
using System.Threading.Tasks;

namespace Clockwise
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <seealso cref="Clockwise.ICommandHandler{T}" />
    internal class AnonymousCommandHandler<T> : ICommandHandler<T>
    {
        private readonly HandleCommand<T> handle;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnonymousCommandHandler{T}"/> class.
        /// </summary>
        /// <param name="handle">The handle.</param>
        /// <exception cref="ArgumentNullException">handle</exception>
        public AnonymousCommandHandler(HandleCommand<T> handle) =>
            this.handle = handle ?? throw new ArgumentNullException(nameof(handle));

        /// <summary>
        /// Handles the delivery.
        /// </summary>
        /// <param name="delivery">The delivery.</param>
        /// <returns></returns>
        public async Task<ICommandDeliveryResult> Handle(ICommandDelivery<T> delivery) =>
            await handle(delivery);
    }
}
