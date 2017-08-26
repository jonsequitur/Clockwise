using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandHandler<T> : ICommandHandler<T>
    {
        private readonly Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle;

        public AnonymousCommandHandler(Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle) =>
            this.handle = handle ?? throw new ArgumentNullException(nameof(handle));

        public async Task<CommandDeliveryResult<T>> Handle(CommandDelivery<T> delivery) =>
            await handle(delivery);
    }
}
