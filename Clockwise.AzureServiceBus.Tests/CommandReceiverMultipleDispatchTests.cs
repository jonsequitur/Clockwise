using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Clockwise.Tests;
using FluentAssertions;
using Pocket;
using Xunit;
using Xunit.Abstractions;

namespace Clockwise.AzureServiceBus.Tests
{
    public class CommandReceiverMultipleDispatchTests : IDisposable
    {
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public VirtualClock Clock { get; } = VirtualClock.Start();

        public CommandReceiverMultipleDispatchTests(ITestOutputHelper output)
        {
            disposables.Add(Clock);
            disposables.Add(LogEvents.Subscribe(e => output.WriteLine(e.ToLogString())));
        }

        public void Dispose() => disposables.Dispose();

        [Fact]
        public async Task A_subscribed_receiver_can_select_a_handler_based_on_message_type()
        {
            var bus = new InMemoryCommandBus<ICommand<CommandTarget>>(Clock);

            var received = new List<ICommand<CommandTarget>>();

            var handler1 = CommandHandler.Create<CreateCommandTarget>(delivery => received.Add(delivery.Command));
            var handler2 = CommandHandler.Create<UpdateCommandTarget>(delivery => received.Add(delivery.Command));

            bus.Subscribe(handler1);
            bus.Subscribe(handler2);

            await bus.Schedule(new CreateCommandTarget(Guid.NewGuid().ToString()));

            await Clock.AdvanceBy(1.Seconds());

            received.Should()
                    .OnlyContain(c => c is CreateCommandTarget)
                    .And
                    .HaveCount(1);
        }
    }
}
