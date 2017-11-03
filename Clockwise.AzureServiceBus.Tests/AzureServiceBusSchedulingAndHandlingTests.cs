using System;
using System.Threading.Tasks;
using Clockwise.Tests;
using FluentAssertions;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Configuration;
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

            Settings.For<string>.Default = new Settings
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
                        message.UserProperties.TryGetValue("TestName", out var test);

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

        protected override void SubscribeHandler<T>(
            Func<ICommandDelivery<T>, ICommandDeliveryResult> handle) =>
            RegisterForDisposal(
                CreateReceiver<T>().Subscribe(
                    CreateHandler(handle))) ;

     

        private MessageReceiver CreateMessageReceiver()
        {
            return new MessageReceiver(serviceBusSettings.ConnectionString,
                                       queueName);
        }

        private MessageSender CreateMessageSender()
        {
            var sender = new MessageSender(serviceBusSettings.ConnectionString,
                                                  queueName);

            sender.RegisterPlugin(new TestHackPlugin());

            return sender;
        }

        protected override ICommandReceiver<T> CreateReceiver<T>()
        {
            var bus = new AzureServiceBusCommandReceiver<T>(
                CreateMessageReceiver());

            RegisterForDisposal(bus);

            return bus;
        }
  
        protected override ICommandScheduler<T> CreateScheduler<T>()
        {
            var bus = new AzureServiceBusCommandScheduler<T>(
                CreateMessageSender());

            RegisterForDisposal(bus);

            return bus;
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
