using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Pocket;

namespace Clockwise.AzureServiceBus
{
    public class AzureServiceBusCommandBus<T> :
        ICommandReceiver<T>,
        ICommandScheduler<T>,
        IDisposable
    {
        private readonly MessageSender messageSender;
        private readonly MessageReceiver messageReceiver;
        private static readonly Logger Log = new Logger("AzureServiceBusCommandBus");

        public AzureServiceBusCommandBus(
            MessageSender messageSender,
            MessageReceiver messageReceiver)
        {
            this.messageSender = messageSender ??
                                 throw new ArgumentNullException(nameof(messageSender));

            this.messageReceiver = messageReceiver ??
                                   throw new ArgumentNullException(nameof(messageReceiver));
        }

        public async Task<ICommandDeliveryResult> Receive(
            Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> handle,
            TimeSpan? timeout = null)
        {
            var received = await messageReceiver.ReceiveAsync(
                               1,
                               timeout ??
                               Settings.For<T>.Default.ReceiveTimeout);

            var message = received?.SingleOrDefault();

            if (message != null)
            {
                return await HandleMessage(handle, message);
            }

            return null;
        }

        public async Task Schedule(ICommandDelivery<T> delivery)
        {
            await messageSender.SendAsync(delivery.ToMessage());
        }

        public IDisposable Subscribe(Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> onNext)
        {
            if (onNext == null)
            {
                throw new ArgumentNullException(nameof(onNext));
            }

            messageReceiver.RegisterMessageHandler(
                async (message, cancellationToken) =>
                {
                    await HandleMessage(onNext, message);
                },
                new MessageHandlerOptions(
                    async args =>
                    {
                        await HandleError(args);
                    })
                {
                    AutoComplete = false,
                    MaxConcurrentCalls = 1
                });

            return Disposable.Empty;
        }

        private static async Task HandleError(ExceptionReceivedEventArgs args)
        {
            Log.Warning("Exception (action: {action})",
                        args.Exception,
                        args.ExceptionReceivedContext.Action);

            await Task.Yield();
        }

        private async Task<ICommandDeliveryResult> HandleMessage(
            Func<CommandDelivery<T>, Task<ICommandDeliveryResult>> onNext,
            Message message)
        {
            var commandDelivery = message.ToCommandDelivery<T>();

            var result = await onNext(commandDelivery);

            switch (result)
            {
                case RetryDeliveryResult<T> _:
                    // await queueClient.AbandonAsync(message.SystemProperties.LockToken);
                    break;

                case CancelDeliveryResult<T> _:
                case CompleteDeliveryResult<T> _:
                    await messageReceiver.CompleteAsync(message.SystemProperties.LockToken);
                    break;
            }

            return result;
        }

        public void Dispose()
        {
            using (Log.OnEnterAndExit())
            {
                Task.Run(messageReceiver.CloseAsync).Wait();
            }
        }
    }
}
