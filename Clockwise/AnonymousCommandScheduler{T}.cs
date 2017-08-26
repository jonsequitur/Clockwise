using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandScheduler<T> : ICommandScheduler<T>
    {
        private readonly Func<CommandDelivery<T>, Task> handle;

        public AnonymousCommandScheduler(Func<CommandDelivery<T>, Task> handle) =>
            this.handle = handle ?? throw new ArgumentNullException(nameof(handle));

        public async Task Schedule(CommandDelivery<T> delivery) =>
            await handle(delivery);
    }
}