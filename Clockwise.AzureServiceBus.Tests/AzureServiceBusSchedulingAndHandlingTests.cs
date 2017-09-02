using System;
using System.Threading.Tasks;
using Clockwise.Tests;
using FluentAssertions;
using Microsoft.Azure.Management.ServiceBus;
using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest.Azure.Authentication;
using Pocket;
using Xunit;
using Xunit.Abstractions;
using static Pocket.Logger;

namespace Clockwise.AzureServiceBus.Tests
{
    [TrackCurrentTest]
    public class AzureServiceBusSchedulingAndHandlingTests : SchedulingAndHandlingTests
    {
        private readonly ServiceBusSettings serviceBusSettings;
        private string queueName = "integration-tests";

        public AzureServiceBusSchedulingAndHandlingTests(ITestOutputHelper output) : base(output)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(@"C:\dev\.config\ServiceBusSettings.json")
                .Build();

            Configuration.For<string>.Default = new Configuration
            {
                RetryPolicy = new RetryPolicy(i => 5.Seconds()),
                ReceiveTimeout = 6.Seconds()
            };

            serviceBusSettings = config.For<ServiceBusSettings>();

            DrainTestQueue().Wait();
        }

        private async Task DrainTestQueue()
        {
            using (var operation = Log.OnEnterAndExit())
            {
                await Task.Delay(5.Seconds());

                operation.Trace("Done with cooldown");

                var receiver = CreateMessageReceiver();

                var messages = await receiver.ReceiveAsync(10, 5.Seconds());

                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        object test;
                        message.UserProperties.TryGetValue("TestName", out test);

                        operation.Trace(
                            "Draining message from source {test}",
                            test);

                        await receiver.CompleteAsync(message.SystemProperties.LockToken);
                    }
                }

                await receiver.CloseAsync();
            }
        }

        protected override ICommandHandler<T> CreateHandler<T>(Func<ICommandDelivery<T>, ICommandDeliveryResult> handle) =>
            CommandHandler
                .Create(handle)
                .RetryOnException()
                .Trace();

        protected override void SubscribeHandler<T>(Func<ICommandDelivery<T>, ICommandDeliveryResult> handle) =>
            RegisterForDisposal(
                CreateReceiver<T>().Subscribe<T>(
                    CreateHandler(handle)));

        private async Task EnsureQueueExists()
        {
            var clientCredential = new ClientCredential(
                serviceBusSettings.ApplicationId,
                serviceBusSettings.ClientSecret);

            var credentials = await ApplicationTokenProvider.LoginSilentAsync(
                                  serviceBusSettings.TenantId,
                                  clientCredential);

            var managementClient = new ServiceBusManagementClient(credentials)
            {
                SubscriptionId = serviceBusSettings.SubscriptionId
            };

            var namespaceName = "Clockwise-tests";

            //           var ns = await managementClient.Namespaces.CreateOrUpdateAsync(
            //                         "jonsequitur-dev",
            //                         namespaceName,
            //                         new SBNamespace
            //                         {
            //                             Location = "WestUS"
            //                         }
            //                     );

            await managementClient.Queues.CreateOrUpdateAsync(
                "jonsequitur-dev",
                namespaceName,
                queueName,
                new SBQueue
                {
                    RequiresDuplicateDetection = true,
                    LockDuration = 1.Minutes()
                }
            );
        }

        private AzureServiceBusCommandBus<T> CreateBus<T>()
        {
            var bus = new AzureServiceBusCommandBus<T>(
                CreateMessageSender(),
                CreateMessageReceiver());

            RegisterForDisposal(bus);

            return bus;
        }

        protected override ICommandScheduler<T> CreateScheduler<T>()
        {
            var bus = CreateBus<T>();

            ICommandScheduler<T> scheduler = bus;

            return scheduler.Trace();
        }

        private MessageSender CreateMessageSender()
        {
            var messageSender = new MessageSender(serviceBusSettings.ConnectionString,
                                                  queueName);

            messageSender.RegisterPlugin(new TestHackPlugin());

            return messageSender;
        }

        private MessageReceiver CreateMessageReceiver()
        {
            return new MessageReceiver(serviceBusSettings.ConnectionString,
                                       queueName);
        }

        protected override ICommandReceiver<T> CreateReceiver<T>()
        {
            var bus = CreateBus<T>();

            ICommandReceiver<T> receiver = bus;

            return receiver.Trace();
        }

        protected override IClock Clock { get; } = new RealtimeClock();

        [Fact]
        public async Task The_Service_Bus_Message_is_available_as_a_property()
        {
            ICommandDelivery<string> received = null;

            var handler = CreateHandler<string>(cmd =>
            {
                received = cmd;
                return cmd.Complete();
            });

            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule("hello!");

            await CreateReceiver<string>().Receive(handler);

            received.Properties["Message"].Should().BeOfType<Message>();
        }
    }

    internal class TestHackPlugin : ServiceBusPlugin
    {
        public override string Name { get; } = nameof(TestHackPlugin);

        public override async Task<Message> BeforeMessageSend(Message message)
        {
            message.UserProperties["TestName"] = TrackCurrentTestAttribute.CurrentTestName;

            return await base.BeforeMessageSend(message);
        }
    }
}
