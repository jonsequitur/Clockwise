using System;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public static class CommandReceiver
    {
        private static readonly Logger receiverLog = new Logger("CommandReceiver");

        public static ICommandReceiver<T> Create<T>(
            Func<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>, TimeSpan?, Task<CommandDeliveryResult<T>>> receive,
            Func<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>, IDisposable> subscribe) =>
            new AnonymousCommandReceiver<T>(receive, subscribe);

        public static async Task<CommandDeliveryResult<T>> Receive<T>(
            this ICommandReceiver<T> receiver,
            ICommandHandler<T> handler,
            TimeSpan? timeout = null) =>
            await receiver.Receive(
                async delivery => await handler.Handle(delivery),
                timeout);

        public static async Task<CommandDeliveryResult<T>> Receive<T>(
            this ICommandReceiver<T> receiver,
            Func<CommandDelivery<T>, CommandDeliveryResult<T>> handle,
            TimeSpan? timeout = null) =>
            await receiver.Receive(
                async delivery => await Task.Run(() => handle(delivery)),
                timeout);

        public static IDisposable Subscribe<T>(
            this ICommandReceiver<T> bus,
            ICommandHandler<T> handler) =>
            bus.Subscribe(async delivery => await handler.Handle(delivery));

        public static ICommandReceiver<T> Trace<T>(
            this ICommandReceiver<T> receiver) =>
            receiver.UseMiddleware(
                receive: async (handle, timeout, next) =>
                {
                    using (receiverLog.OnEnterAndExit("Receive"))
                    {
                        return await next(handle, timeout);
                    }
                },
                subscribe: (onNext, next) =>
                {
                    using (receiverLog.OnEnterAndExit("Subscribe"))
                    {
                        return next(onNext);
                    }
                });

        public static ICommandReceiver<T> UseMiddleware<T>(
            this ICommandReceiver<T> receiver,
            CommandReceivingMiddleware<T> receive,
            CommandSubscribingMiddleware<T> subscribe) =>
            Create<T>(
                receive:
                (handle, timeout) =>
                {
                    return receive(handle, timeout, receiver.Receive);
                },
                subscribe: onNext => subscribe(onNext, receiver.Subscribe));
    }
}
