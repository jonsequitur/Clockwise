using System;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Core;
using Pocket;

namespace Clockwise.AzureServiceBus
{
    public class AzureServiceBusCommandScheduler<T> :
        ICommandScheduler<T>,
        IDisposable
    {
        private static readonly Logger Log = new Logger<AzureServiceBusCommandScheduler<T>>();
        private readonly MessageSender messageSender;

        public AzureServiceBusCommandScheduler(
            MessageSender messageSender)
        {
            this.messageSender = messageSender ??
                                 throw new ArgumentNullException(nameof(messageSender));
        }

        public async Task Schedule(ICommandDelivery<T> delivery) =>
            await messageSender.SendAsync(delivery.ToMessage());

        public void Dispose()
        {
            using (Log.OnEnterAndExit())
            {
                Task.Run(messageSender.CloseAsync).Wait();
            }
        }
    }
}
