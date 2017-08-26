using System;
using System.Text;
using Microsoft.Azure.ServiceBus;

namespace Clockwise.AzureServiceBus
{
    public static class CommandDeliveryExtensions
    {
        public static Message ToMessage<T>(this CommandDelivery<T> delivery)
        {
            var json = delivery.Command.ToJson();

            var message = new Message
            {
                Body = Encoding.UTF8.GetBytes(json),
                ContentType = "application/json",
                MessageId = delivery.IdempotencyToken
            };

            if (delivery.DueTime != null)
            {
                message.ScheduledEnqueueTimeUtc = delivery.DueTime.Value.UtcDateTime;
            }

            return message;
        }

        public static CommandDelivery<T> ToCommandDelivery<T>(this Message message)
        {
            var commandJson = Encoding.UTF8.GetString(message.Body);

            var command = commandJson.FromJsonTo<T>();

            var enqueuedTimeUtc = message.SystemProperties.EnqueuedTimeUtc;

            var delivery = new CommandDelivery<T>(
                command,
                dueTime: enqueuedTimeUtc,
                originalDueTime: enqueuedTimeUtc,
                idempotencyToken: message.MessageId,
                numberOfPreviousAttempts: message.SystemProperties.DeliveryCount - 1);

            delivery.Properties["Message"] = message;

            return delivery;
        }
    }
}
