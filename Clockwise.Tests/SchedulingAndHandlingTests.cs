using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Pocket;
using Xunit;
using Xunit.Abstractions;

namespace Clockwise.Tests
{
    public abstract class SchedulingAndHandlingTests : IDisposable
    {
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        protected SchedulingAndHandlingTests(ITestOutputHelper output)
        {
            disposables.Add(LogEvents.Subscribe(e => output.WriteLine(e.ToLogString())));
            disposables.Add(Disposable.Create(Configuration.For<string>.Reset));
        }

        public virtual void Dispose() => disposables.Dispose();

        protected void RegisterForDisposal(IDisposable disposable) => disposables.Add(disposable);

        [Fact]
        public async Task A_command_can_be_scheduled_for_asap_delivery()
        {
            CommandDelivery<string> received = null;

            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule("now!");

            var receiver = CreateReceiver<string>();

            await receiver.Receive(
                CreateHandler<string>(cmd =>
                {
                    received = cmd;
                    return cmd.Complete();
                }));

            received.Command.Should().Be("now!");
        }

        [Fact]
        public async Task A_command_can_be_scheduled_for_future_delivery()
        {
            CommandDelivery<string> received = null;

            SubscribeHandler<string>(cmd =>
            {
                received = cmd;
                return cmd.Complete();
            });

            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule(
                "hello eventually!",
                dueTime: Clock.Now().AddSeconds(10));

            await Clock.Wait(5.Seconds());

            if (received != null)
            {
                throw new Exception("Message arrived sooner than expected: " + received.Command);
            }

            await Clock.Wait(5.Seconds());

            received.Command.Should().Be("hello eventually!");
        }

        [Fact]
        public async Task Receive_can_be_used_to_receive_just_one_message()
        {
            var receivedCommands = new List<string>();
            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule("first!");
            await scheduler.Schedule("too late");

            var handler = CreateHandler<string>(delivery =>
            {
                receivedCommands.Add(delivery.Command);
                return delivery.Complete();
            });

            var receiver = CreateReceiver<string>();

            await receiver.Receive(handler);

            receivedCommands.Should().HaveCount(1);
            receivedCommands.Should().Contain("first!");
        }

        [Fact]
        public async Task Receive_can_specify_a_timeout_after_which_it_returns_if_no_message_was_received()
        {
            var receivedCount = 0;

            var handler = CreateHandler<string>(delivery =>
            {
                receivedCount++;
                return delivery.Complete();
            });

            var receiver = CreateReceiver<string>();

            await receiver.Receive(handler, 2.Seconds());

            receivedCount.Should().Be(0);
        }

        [Fact]
        public async Task When_an_action_is_scheduled_in_the_past_it_is_executed_as_soon_as_possible()
        {
            var received = false;

            var scheduler = CreateScheduler<string>();

            var handler = CreateHandler<string>(cmd =>
            {
                received = true;
                return cmd.Complete();
            });

            await scheduler.Schedule("sorry, i'm late", dueTime: Clock.Now().Subtract(1.Days()));

            await CreateReceiver<string>().Receive(handler);

            received.Should().BeTrue();
        }

        [Fact]
        public async Task When_a_command_is_scheduled_before_a_handler_is_registered_then_it_is_still_delivered_later()
        {
            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule("i'll wait...");

            // advance the clock past the due time before creating a handler
            await Clock.Wait(2.Seconds());

            var received = false;

            var handler = CreateHandler<string>(delivery =>
            {
                received = true;
                return delivery.Complete();
            });

            await CreateReceiver<string>().Receive(handler);

            received.Should().BeTrue();
        }

        [Fact]
        public async Task When_an_exception_is_thrown_then_the_command_will_be_retried()
        {
            var success = false;

            var handler = CreateHandler<string>(cmd =>
            {
                // first try...
                if (cmd.NumberOfPreviousAttempts == 0)
                {
                    throw new Exception("drat!");
                }

                // second try...
                success = true;

                return cmd.Complete();
            });

            await CreateScheduler<string>().Schedule("hi!");

            var receiver = CreateReceiver<string>();

            // first try
            await receiver.Receive(handler);

            // second try
            await receiver.Receive(handler);

            success.Should().BeTrue();
        }

        [Fact]
        public async Task When_an_exception_is_thrown_too_many_times_then_the_command_will_not_be_retried()
        {
            Configuration.For<string>.Default.RetryPolicy = 
                new RetryPolicy(previousAttempts =>
                {
                    if (previousAttempts < 1)
                    {
                        return 5.Seconds();
                    }

                    return new TimeSpan?();
                });

            var deliveryCount = 0;

          var handler =  CreateHandler<string>(cmd =>
            {
                deliveryCount++;
                throw new Exception("oops!");
            });

            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule("howdy!");

            var receiver = CreateReceiver<string>();

            await receiver.Receive(handler);
            await receiver.Receive(handler);
            await receiver.Receive(handler);

            deliveryCount.Should().Be(2);
        }

        [Fact]
        public async Task A_command_can_be_scheduled_for_retry()
        {
            var receivedTimes = new List<DateTimeOffset>();

            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule("keep trying...");

            var handler = CreateHandler<string>(cmd =>
            {
                receivedTimes.Add(Clock.Now());
                return cmd.Retry(5.Seconds());
            });

            var receiver = CreateReceiver<string>();

            await receiver.Receive(handler);
            await receiver.Receive(handler);

            receivedTimes.Should().HaveCount(2);
            receivedTimes[1].Should().BeCloseTo(receivedTimes[0] + 5.Seconds(), precision: 1000);
        }

        [Fact]
        public async Task When_a_command_is_canceled_it_is_not_redelivered()
        {
            var deliveryCount = 0;

            var handler = CreateHandler<string>(cmd =>
            {
                deliveryCount++;
                return cmd.Cancel();
            });

            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule(nameof(When_a_command_is_completed_it_is_not_redelivered));

            var receiver = CreateReceiver<string>();

            await receiver.Receive(handler);
            await receiver.Receive(handler);

            deliveryCount.Should().Be(1);
        }

        [Fact]
        public async Task When_a_command_is_completed_it_is_not_redelivered()
        {
            var deliveryCount = 0;

            var handler = CreateHandler<string>(cmd =>
            {
                deliveryCount++;
                return cmd.Complete();
            });

            var scheduler = CreateScheduler<string>();

            await scheduler.Schedule(nameof(When_a_command_is_completed_it_is_not_redelivered));

            var receiver = CreateReceiver<string>();

            await receiver.Receive(handler);
            await receiver.Receive(handler);

            deliveryCount.Should().Be(1);
        }

        [Fact]
        public async Task When_a_command_is_scheduled_with_the_same_idempotency_token_twice_it_is_not_delivered_twice()
        {
            var received = new List<string>();

            var scheduler = CreateScheduler<string>();

            var idempotencyToken = Guid.NewGuid().ToString();

            await scheduler.Schedule("not redundant", idempotencyToken: idempotencyToken);

            await scheduler.Schedule("redundant", idempotencyToken: idempotencyToken);

            SubscribeHandler<string>(cmd =>
            {
                received.Add(cmd.Command);
                return cmd.Complete();
            });

            await Clock.Wait(5.Seconds());

            received.Should().HaveCount(1);
            received.Should().Contain("not redundant");
        }

        [Fact]
        public async Task When_messages_are_deduplicated_then_the_winner_is_the_first_to_be_scheduled()
        {
            var received = new List<string>();

            var scheduler = CreateScheduler<string>();

            var sooner = Clock.Now().AddSeconds(2);
            var later = Clock.Now().AddSeconds(5);

            var idempotencyToken = Guid.NewGuid().ToString();

            await scheduler.Schedule(
                "scheduled first",
                dueTime: later,
                idempotencyToken: idempotencyToken);

            await scheduler.Schedule(
                "scheduled second",
                dueTime: sooner,
                idempotencyToken: idempotencyToken);

            var handler = CreateHandler<string>(cmd =>
            {
                received.Add(cmd.Command);
                return cmd.Complete();
            });

            await CreateReceiver<string>().Receive(handler, 10.Seconds());

            received.Should().HaveCount(1);
            received.Should().Contain("scheduled first");
        }

        protected abstract void SubscribeHandler<T>(
            Func<CommandDelivery<T>, CommandDeliveryResult<T>> handle);

        protected abstract ICommandScheduler<T> CreateScheduler<T>();

        protected abstract IClock Clock { get; }

        protected abstract ICommandHandler<T> CreateHandler<T>(
            Func<CommandDelivery<T>, CommandDeliveryResult<T>> handle);

        protected abstract ICommandReceiver<T> CreateReceiver<T>();
    }
}
