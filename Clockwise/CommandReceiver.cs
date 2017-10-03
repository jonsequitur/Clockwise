using System;
using System.Linq;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public static class CommandReceiver
    {
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
                    ICommandDelivery<T> delivery = null;

                    using (For<T>.Log("Receive",
                                      () => new (string, object)[] { ("delivery", delivery) }))
                    {
                        return await next(d =>
                        {
                            delivery = d;
                            return handle(d);
                        }, timeout);
                    }
                },
                subscribe: (onNext, next) =>
                {
                    using (For<T>.Log("Subscribe"))
                    {
                        return next(delivery =>
                        {
                            using (For<T>.Log("Receive", args: new object[] { delivery }))
                            {
                                return onNext(delivery);
                            }
                        });
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
                subscribe: onNext =>
                {
                    return subscribe(onNext, receiver.Subscribe);
                });

        private static class For<T>
        {
            private static readonly string category = $"{nameof(CommandReceiver)}<{typeof(T).Name}>";

            public static OperationLogger Log(
                string operationName,
                Func<(string name, object value)[]> exitArgs = null,
                object[] args = null) => new OperationLogger(
                operationName,
                category,
                args: args,
                exitArgs: exitArgs,
                logOnStart: true);
        }
    }
}
