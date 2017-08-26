using System;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public static class CommandHandler
    {
        private static readonly Logger handlerLog = new Logger("CommandHandler");

        internal static ConfirmationLogger CommandDelivery<T>(
            this Logger logger,
            CommandDelivery<T> delivery) =>
            new ConfirmationLogger(
                $"Handle:{typeof(T).Name}",
                logger.Category,
                null,
                null,
                true);

        public static ICommandHandler<T> Create<T>(Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle) =>
            new AnonymousCommandHandler<T>(handle);

        public static ICommandHandler<T> Create<T>(Func<CommandDelivery<T>, CommandDeliveryResult<T>> handle) =>
            new AnonymousCommandHandler<T>(d => Task.Run(() => handle(d)));

        public static ICommandHandler<T> Create<T>(Action<CommandDelivery<T>> handle) =>
            new AnonymousCommandHandler<T>(delivery =>
            {
                handle(delivery);
                return Task.FromResult<CommandDeliveryResult<T>>(delivery.Complete());
            });

        public static ICommandHandler<T> RetryOnException<T>(
            this ICommandHandler<T> handler) =>
            handler.UseMiddleware(async (delivery, next) =>
            {
                try
                {
                    return await next(delivery);
                }
                catch (Exception exception)
                {
                    var retryPolicy = Configuration.For<T>.Default.RetryPolicy;

                    var retryPeriod = retryPolicy.RetryPeriodAfter(delivery.NumberOfPreviousAttempts);

                    if (retryPeriod != null)
                    {
                        var retry = delivery.Retry();

                        retry.SetException(exception);

                        return retry;
                    }
                    else
                    {
                        return delivery.Cancel("Retry attempts exhausted", exception);
                    }
                }
            });

        public static ICommandHandler<T> Trace<T>(
            this ICommandHandler<T> handler) =>
            handler.UseMiddleware(async (delivery, next) =>
            {
                using (var operation = handlerLog.CommandDelivery(delivery))
                {
                    var result = await next(delivery);

                    switch (result)
                    {
                        case CompleteDeliveryResult<T> _:
                            operation.Succeed("Completed: {command}", delivery.Command);
                            break;

                        case RetryDeliveryResult<T> retry:
                            operation.Fail(
                                retry?.Exception,
                                "Will retry: {command}",
                                delivery.Command);
                            break;
                        case CancelDeliveryResult<T> cancel:
                            operation.Fail(
                                cancel?.Exception,
                                "Canceled: {command}",
                                delivery.Command);
                            break;
                    }

                    return result;
                }
            });

        public static ICommandHandler<T> UseMiddleware<T>(
            this ICommandHandler<T> handler,
            CommandHandlingMiddleware<T> middleware) =>
            Create<T>(async delivery =>
                          await middleware(
                              delivery,
                              async d => await handler.Handle(d)));
    }

    public delegate Task<CommandDeliveryResult<T>> CommandHandlingMiddleware<T>(
        CommandDelivery<T> delivery,
        Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> next);

    public delegate Task CommandSchedulingMiddleware<T>(
        CommandDelivery<T> delivery,
        Func<CommandDelivery<T>, Task> next);

    public delegate IDisposable CommandSubscribingMiddleware<T>(
        Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> onNext,
        Func<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>, IDisposable> next);

    public delegate Task<CommandDeliveryResult<T>> CommandReceivingMiddleware<T>(
        Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle,
        TimeSpan? timeout,
        Func<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>, TimeSpan?, Task<CommandDeliveryResult<T>>> next);
}
