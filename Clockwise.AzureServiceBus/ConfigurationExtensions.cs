using System;
using System.Reflection;
using Microsoft.Azure.ServiceBus.Core;
using Pocket;

namespace Clockwise.AzureServiceBus
{
    public static class ConfigurationExtensions
    {
        private static readonly MethodInfo createReceiverMethod = typeof(ConfigurationExtensions)
            .GetMethod(nameof(CreateReceiver), BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo createSchedulerMethod = typeof(ConfigurationExtensions)
            .GetMethod(nameof(CreateScheduler), BindingFlags.NonPublic | BindingFlags.Static);

        public static Configuration UseAzureServiceBus(
            this Configuration configuration,
            string connectionString)
        {
            configuration.AddReceiverStrategy(connectionString);

            configuration.AddSchedulerStrategy(connectionString);

            return configuration;
        }

        private static void AddReceiverStrategy(
            this Configuration configuration,
            string connectionString) =>
            configuration.Container
                         .AddStrategy(type =>
                         {
                             if (type.IsGenericType &&
                                 type.GetGenericTypeDefinition() == typeof(ICommandReceiver<>))
                             {
                                 var commandType = type.GenericTypeArguments[0];

                                 var create = createReceiverMethod
                                                  .MakeGenericMethod(commandType)
                                                  .CreateDelegate(typeof(Func<MessageReceiver, object>)) as Func<MessageReceiver, object>;

                                 var queueName = commandType.Name;
                                 var receiver = new MessageReceiver(connectionString, queueName);

                                 return Create;

                                 object Create(PocketContainer c) => create(receiver);
                             }

                             return null;
                         });

        private static void AddSchedulerStrategy(
            this Configuration configuration,
            string connectionString) =>
            configuration.Container
                         .AddStrategy(type =>
                         {
                             if (type.IsGenericType &&
                                 type.GetGenericTypeDefinition() == typeof(ICommandScheduler<>))
                             {
                                 var commandType = type.GenericTypeArguments[0];

                                 var create = createSchedulerMethod
                                                  .MakeGenericMethod(commandType)
                                                  .CreateDelegate(typeof(Func<MessageSender, object>)) as Func<MessageSender, object>;

                                 var queueName = commandType.Name;
                                 var sender = new MessageSender(connectionString, queueName);

                                 return Create;

                                 object Create(PocketContainer c) => create(sender);
                             }

                             return null;
                         });

        private static AzureServiceBusCommandReceiver<T> CreateReceiver<T>(MessageReceiver messageReceiver) =>
            new AzureServiceBusCommandReceiver<T>(messageReceiver);

        private static AzureServiceBusCommandScheduler<T> CreateScheduler<T>(MessageSender messageSender) =>
            new AzureServiceBusCommandScheduler<T>(messageSender);
    }
}
