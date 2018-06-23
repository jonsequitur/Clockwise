using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandHandler<T> : ICommandHandler<T>
    {
        private readonly CommandHandler<T> handle;

        public AnonymousCommandHandler(CommandHandler<T> handle) =>
            this.handle = handle ?? throw new ArgumentNullException(nameof(handle));

        public async Task<ICommandDeliveryResult> Handle(ICommandDelivery<T> delivery) =>
            await handle(delivery);
    }
}
