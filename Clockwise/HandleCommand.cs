using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public static class HandleCommand
    {
        public static ICommandHandler<T> Create<T>(HandleCommand<T> handle) =>
            new AnonymousCommandHandler<T>(handle);

        public static ICommandHandler<T> Create<T>(Func<ICommandDelivery<T>, ICommandDeliveryResult> handle) =>
            new AnonymousCommandHandler<T>(d => Task.Run(() => handle(d)));

        public static ICommandHandler<T> Create<T>(Action<ICommandDelivery<T>> handle) =>
            new AnonymousCommandHandler<T>(delivery =>
            {
                handle(delivery);
                return Task.FromResult<ICommandDeliveryResult>(delivery.Complete());
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
                    var retryPolicy = Settings.For<T>.Default.RetryPolicy;

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
                using (var operation = Log.Handle(delivery))
                {
                    var result = await next(delivery);

                    Log.Handled(operation, delivery, result);

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

    public delegate Task<ICommandDeliveryResult> HandleCommand<in T> (ICommandDelivery<T> delivery);

    public delegate Task<ICommandDeliveryResult> CommandHandlingMiddleware<T>(
        ICommandDelivery<T> delivery,
        HandleCommand<T> handle);

    public delegate Task CommandSchedulingMiddleware<T>(
        ICommandDelivery<T> delivery,
        Func<ICommandDelivery<T>, Task> schedule);

    public delegate IDisposable CommandSubscribingMiddleware<T>(
        HandleCommand<T> handle,
        Func<HandleCommand<T>, IDisposable> subscribe);

    public delegate Task<ICommandDeliveryResult> CommandReceivingMiddleware<T>(
        HandleCommand<T> handle,
        TimeSpan? timeout,
        Func<HandleCommand<T>, TimeSpan?, Task<ICommandDeliveryResult>> receive);
}
