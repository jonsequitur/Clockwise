using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.Azure.ServiceBus;
using Pocket;
using Xunit;
using Xunit.Abstractions;

namespace Clockwise.AzureServiceBus.Tests
{
    public class SerializationExtensionsTests : IDisposable
    {
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public SerializationExtensionsTests(ITestOutputHelper output)
        {
            disposables.Add(LogEvents.Subscribe(e => output.WriteLine(e.ToLogString())));
        }

        public void Dispose() => disposables.Dispose();

        [Fact]
        public void CommandDelivery_Command_can_be_round_tripped_through_service_bus_message()
        {
            var original = new CommandDelivery<MyMessageClass>(
                new MyMessageClass(
                    stringProperty: "oh hello",
                    dateProperty: Clock.Now().Add(2.Days()),
                    nullableDateProperty: Clock.Now().Add(3.Hours()),
                    boolProperty: true,
                    nullableBoolProperty: true,
                    intProperty: 123,
                    nullableIntProperty: 456),
                dueTime: Clock.Now().AddDays(1));

            var message = original.ToMessage();

            SimulateMessageHavingBeenReceived(message);

            var deserialized = message.ToCommandDelivery<MyMessageClass>();

            deserialized.Command.ShouldBeEquivalentTo(original.Command);
        }

        [Fact]
        public void CommandDelivery_properties_are_initialized_properly_from_a_received_Message()
        {
            using (VirtualClock.Start())
            {
                var message = new CommandDelivery<string>(
                    "hi",
                    dueTime: Clock.Now()).ToMessage();

                SimulateMessageHavingBeenReceived(
                    message,
                    enqueuedTimeUtc: Clock.Now().UtcDateTime);

                var deserialized = message.ToCommandDelivery<string>();

                deserialized
                    .DueTime
                    .Value
                    .UtcDateTime
                    .ShouldBeEquivalentTo(
                        message.SystemProperties
                               .EnqueuedTimeUtc);
                deserialized
                    .OriginalDueTime
                    .Should()
                    .Be(
                        message.SystemProperties
                               .EnqueuedTimeUtc);
                deserialized
                    .NumberOfPreviousAttempts
                    .Should()
                    .Be(
                        message
                            .SystemProperties
                            .DeliveryCount - 1);
            }
        }

        private static void SimulateMessageHavingBeenReceived(
            Message message,
            int deliveryCount = 1,
            DateTime? enqueuedTimeUtc = null)
        {
            // set the sequence number which triggers these properties' being gettable, otherwise it throws  
            var propertyInfo = typeof(Message.SystemPropertiesCollection)
                .GetProperty(nameof(message.SystemProperties.SequenceNumber))
                .GetSetMethod(true);

            propertyInfo.Invoke(message.SystemProperties, new object[] { 1 });

            propertyInfo = typeof(Message.SystemPropertiesCollection)
                .GetProperty(nameof(message.SystemProperties.DeliveryCount))
                .GetSetMethod(true);

            propertyInfo.Invoke(message.SystemProperties, new object[] { deliveryCount });

            if (enqueuedTimeUtc != null)
            {
                propertyInfo = typeof(Message.SystemPropertiesCollection)
                    .GetProperty(nameof(message.SystemProperties.EnqueuedTimeUtc))
                    .GetSetMethod(true);

                propertyInfo.Invoke(message.SystemProperties,
                                    new object[] { enqueuedTimeUtc });
            }
        }
    }

    public class MyMessageClass
    {
        public MyMessageClass(
            string stringProperty,
            DateTimeOffset dateProperty,
            DateTimeOffset? nullableDateProperty,
            bool boolProperty,
            bool? nullableBoolProperty,
            int intProperty,
            int? nullableIntProperty)
        {
            StringProperty = stringProperty;
            DateProperty = dateProperty;
            NullableDateProperty = nullableDateProperty;
            BoolProperty = boolProperty;
            NullableBoolProperty = nullableBoolProperty;
            IntProperty = intProperty;
            NullableIntProperty = nullableIntProperty;
        }

        public string StringProperty { get; }

        public DateTimeOffset DateProperty { get; }
        public DateTimeOffset? NullableDateProperty { get; }

        public bool BoolProperty { get; }
        public bool? NullableBoolProperty { get; }

        public int IntProperty { get; }
        public int? NullableIntProperty { get; }
    }
}
