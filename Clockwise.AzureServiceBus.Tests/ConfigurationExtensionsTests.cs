using System;
using FluentAssertions;
using Xunit;

namespace Clockwise.AzureServiceBus.Tests
{
    public class ConfigurationExtensionsTests
    {
        private readonly string fakeConnectiongString = "Endpoint=sb://clockwise-sample.servicebus.windows.net/;SharedAccessKeyName=clockwise;SharedAccessKey=something";

        [Fact]
        public void UseAzureServiceBus_enables_scheduler_instances_to_be_obtained_from_Configuration()
        {
            using (var configuration = new Configuration()
                .UseAzureServiceBus(fakeConnectiongString))
            {
                var scheduler = configuration.CommandScheduler<string>();

                scheduler.Should()
                         .BeOfType<AzureServiceBusCommandScheduler<string>>();
            }
        }

        [Fact]
        public void UseAzureServiceBus_enables_receiver_instances_to_be_obtained_from_Configuration()
        {
            using (var configuration = new Configuration()
                .UseAzureServiceBus(fakeConnectiongString))
            {
                var receiver = configuration.CommandReceiver<string>();

                receiver.Should()
                        .BeOfType<AzureServiceBusCommandReceiver<string>>();
            }
        }
    }
}
