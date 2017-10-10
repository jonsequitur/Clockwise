using System;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public static class CommandScheduler
    {
        public static ICommandScheduler<T> Create<T>(
            Func<ICommandDelivery<T>, Task> handle) =>
            new AnonymousCommandScheduler<T>(handle);

        public static ICommandScheduler<T> Create<T>(Action<ICommandDelivery<T>> handle) =>
            new AnonymousCommandScheduler<T>(delivery =>
            {
                handle(delivery);
                return Task.FromResult(true);
            });

        public static async Task Schedule<T>(
            this ICommandScheduler<T> scheduler,
            T command,
            DateTimeOffset? dueTime = null,
            string idempotencyToken = null) =>
            await scheduler.Schedule(
                new CommandDelivery<T>(
                    command,
                    dueTime,
                    idempotencyToken: idempotencyToken));

        public static async Task Schedule<T>(
            this ICommandScheduler<T> scheduler,
            T command,
            TimeSpan dueAfter,
            string idempotencyToken = null) =>
            await scheduler.Schedule(
                command,
                Clock.Current.Now().Add(dueAfter));

        public static ICommandScheduler<T> Trace<T>(
            this ICommandScheduler<T> scheduler) =>
            scheduler.UseMiddleware(async (delivery, next) =>
            {
                using (new OperationLogger(
                    "Schedule",
                    $"CommandScheduler<{typeof(T).Name}>",
                    message: "{delivery}",
                    args: new object[] { delivery },
                    logOnStart: true))
                {
                    await next(delivery);
                }
            });

        public static ICommandScheduler<T> UseMiddleware<T>(
            this ICommandScheduler<T> scheduler,
            CommandSchedulingMiddleware<T> middleware) =>
            Create<T>(async delivery => await middleware(
                                            delivery,
                                            async d => await scheduler.Schedule(d)));

     
    }
}
