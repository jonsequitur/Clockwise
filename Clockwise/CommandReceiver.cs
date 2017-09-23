using System;
using System.Linq;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public static class CommandReceiver
    {
        private static readonly Logger receiverLog = new Logger("CommandReceiver");

        internal static ICommandReceiver<T> Create<T>(
            Func<Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>, TimeSpan?, Task<ICommandDeliveryResult>> receive,
            Func<Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>, IDisposable> subscribe) =>
            new AnonymousCommandReceiver<T>(receive, subscribe);

        public static async Task<ICommandDeliveryResult> Receive<T>(
            this ICommandReceiver<T> receiver,
            ICommandHandler<T> handler,
            TimeSpan? timeout = null) =>
            await receiver.Receive(
                async delivery => await handler.Handle(delivery),
                timeout);

        public static async Task<ICommandDeliveryResult> Receive<T>(
            this ICommandReceiver<T> receiver,
            Func<ICommandDelivery<T>, ICommandDeliveryResult> handle,
            TimeSpan? timeout = null) =>
            await receiver.Receive(
                async delivery => await Task.Run(() => handle(delivery)),
                timeout);

        public static IDisposable Subscribe<TReceive, THandle>(
            this ICommandReceiver<TReceive> receiver,
            ICommandHandler<THandle> handler)
            where THandle : class, TReceive
        {
            async Task<ICommandDeliveryResult> OnNext(ICommandDelivery<TReceive> delivery)
            {
                switch (delivery)
                {
                    case ICommandDelivery<THandle> handled:
                        return await handler.Handle(handled);

                    default:
                        if (!(delivery.Command is THandle command))
                        {
                            return null;
                        }

                        var clone = new CommandDelivery<THandle>(
                            command,
                            delivery.DueTime,
                            delivery.OriginalDueTime,
                            delivery.IdempotencyToken,
                            delivery.NumberOfPreviousAttempts);

                        return await handler.Handle(clone);
                }
            }

            return receiver.Subscribe(OnNext);
        }

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
                (handle, timeout) => receive(handle, timeout, receiver.Receive),
                subscribe: onNext => subscribe(onNext, receiver.Subscribe));
    }
}
