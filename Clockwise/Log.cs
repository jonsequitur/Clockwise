using Pocket;

namespace Clockwise
{
    internal static class Log
    {
        private static class Category<T>
        {
            public static readonly string Handler = $"{nameof(CommandHandler)}<{typeof(T).Name}>";
            public static readonly string Receiver = $"{nameof(CommandReceiver)}<{typeof(T).Name}>";
            public static readonly string Scheduler = $"{nameof(CommandScheduler)}<{typeof(T).Name}>";
        }

        private static string messageTemplate = "{Command} ({IdempotencyToken}) due @ {DueTime}. previous attempts: {NumberOfPreviousAttempts} ]";

        public static ConfirmationLogger Handle<T>(ICommandDelivery<T> delivery) =>
            new ConfirmationLogger(
                "Handle",
                Category<T>.Handler,
                messageTemplate,
                args: Destructure(delivery),
                logOnStart: true);

        public static ConfirmationLogger Receive<T>(ICommandDelivery<T> delivery) =>
            new ConfirmationLogger(
                "Receive",
                Category<T>.Receiver,
                messageTemplate,
                args: Destructure(delivery),
                logOnStart: true);

        public static ConfirmationLogger Schedule<T>(ICommandDelivery<T> delivery) => new ConfirmationLogger(
            "Schedule",
            Category<T>.Scheduler,
            messageTemplate,
            args: Destructure(delivery),
            logOnStart: true);

        private static object[] Destructure<T>(this ICommandDelivery<T> delivery) =>
            new object[]
            {
                delivery.Command,
                delivery.IdempotencyToken,
                delivery.DueTime,
                delivery.NumberOfPreviousAttempts
            };

        public static OperationLogger Subscribe<T>() =>
            new OperationLogger(
                "Subscribe",
                Category<T>.Receiver,
                logOnStart: true);

        public static void Completion<T>(
            ConfirmationLogger operation,
            ICommandDelivery<T> delivery,
            ICommandDeliveryResult result)
        {
            string resultString = null;

            switch (result)
            {
                case CompleteDeliveryResult<T> complete:
                    resultString = "Complete";
                    break;
                case RetryDeliveryResult<T> retry:
                    resultString = "WillRetry";
                    break;
                case CancelDeliveryResult<T> cancel:
                    resultString = "Cancelled";
                    break;
            }

            operation.Succeed(
                $"{{result}}: {messageTemplate}",
                resultString,
                delivery.Command,
                delivery.IdempotencyToken,
                delivery.DueTime,
                delivery.NumberOfPreviousAttempts);
        }

        public static void Completion<T>(
            ConfirmationLogger operation,
            ICommandDelivery<T> delivery) =>
            operation.Succeed(
                "{Command} :{IdempotencyToken} due @ {DueTime}",
                delivery.Command,
                delivery.IdempotencyToken,
                delivery.DueTime);
    }
}
