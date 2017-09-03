using System;
using System.Threading.Tasks;

namespace Clockwise
{
    internal class AnonymousCommandScheduler<T> : ICommandScheduler<T>
    {
        private readonly Func<ICommandDelivery<T>, Task> handle;

        public AnonymousCommandScheduler(Func<ICommandDelivery<T>, Task> handle) =>
            this.handle = handle ?? throw new ArgumentNullException(nameof(handle));

        public async Task Schedule(ICommandDelivery<T> delivery) =>
            await handle(delivery);
    }
}