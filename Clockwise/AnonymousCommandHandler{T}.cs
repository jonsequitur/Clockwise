using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandHandler<T> : ICommandHandler<T>
    {
        private readonly Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle;

        public AnonymousCommandHandler(Func<ICommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle) =>
            this.handle = handle ?? throw new ArgumentNullException(nameof(handle));

        public async Task<CommandDeliveryResult<T>> Handle(ICommandDelivery<T> delivery) =>
            await handle(delivery);
    }
}
