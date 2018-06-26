using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public static class CommandReceiver
    {
        /// <summary>
        /// Creates the an instance of <see cref="ICommandReceiver{T}"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="receive">The receive.</param>
        /// <param name="subscribe">The subscribe.</param>
        /// <returns></returns>
        internal static ICommandReceiver<T> Create<T>(
            Func<HandleCommand<T>, TimeSpan?, Task<ICommandDeliveryResult>> receive,
            Func<HandleCommand<T>, IDisposable> subscribe) =>
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
            where THandle : TReceive
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

        /// <summary>
        /// Enables tracing on the specified <see cref="ICommandReceiver{T}"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="receiver">The receiver.</param>
        /// <returns></returns>
        public static ICommandReceiver<T> Trace<T>(
                    this ICommandReceiver<T> receiver) =>
                    receiver.UseMiddleware(
                        receive: async (handle, timeout, next) =>
                        {
                            return await next(async delivery =>
                            {
                                using (var operation = Log.Receive(delivery))
                                {
                                    var result = await handle(delivery);

                                    Log.Handled(operation, delivery, result);

                                    return result;
                                }
                            }, timeout);
                        },
                        subscribe: (handle, subscribe) =>
                        {
                            using (Log.Subscribe<T>())
                            {
                                return subscribe(async delivery =>
                                {
                                    using (var operation1 = Log.Receive(delivery))
                                    {
                                        var deliveryResult = await handle(delivery);

                                        Log.Handled(operation1, delivery, deliveryResult);

                                        return deliveryResult;
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
                (handle, timeout) => receive(handle, timeout, receiver.Receive),
                subscribe: onNext => subscribe(onNext, receiver.Subscribe));
    }
}
