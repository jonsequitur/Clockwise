using System;
using FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
using Pocket;
using Xunit;
using Xunit.Abstractions;

namespace Clockwise.Tests
{
    public class DiagnosticTests : IDisposable
    {
        private readonly Configuration configuration;
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private readonly LogEntryList log = new LogEntryList();

        public DiagnosticTests(ITestOutputHelper output)
        {
            configuration = new Configuration()
                .UseInMemoryScheduling()
                .TraceCommands();
            disposables.Add(configuration);
            disposables.Add(VirtualClock.Start());

            var fromAssemblies = new[] { GetType().Assembly, typeof(ICommandScheduler<>).Assembly };
            disposables.Add(LogEvents.Subscribe(e => output.WriteLine(e.ToLogString()), fromAssemblies));
            disposables.Add(LogEvents.Subscribe(log.Add, fromAssemblies));
        }

        public void Dispose() => disposables.Dispose();

        [Fact]
        public async Task Trace_enables_diagnostic_output_on_all_schedulers_when_Schedule_is_called()
        {
            configuration.TraceCommands();

            var scheduler = configuration.CommandScheduler<string>();

            await scheduler.Schedule(new CommandDelivery<string>(
                                         "hi!",
                                         dueTime: DateTimeOffset.Parse("1/31/2017 12:05am +00:00"),
                                         idempotencyToken: "the-idempotency-token"));

            log.Should()
               .ContainSingle(e => e.Category == "CommandScheduler<String>" &&
                                   e.OperationName == "Schedule" &&
                                   e.Operation.IsStart);
            log.Should()
               .ContainSingle(e => e.Category == "CommandScheduler<String>" &&
                                   e.OperationName == "Schedule" &&
                                   e.Operation.IsEnd);
        }

        [Fact]
        public async Task Trace_enables_diagnostic_output_on_discovered_handlers()
        {
            configuration
                .TraceCommands()
                .UseDependency<IStore<CommandTarget>>(_ => new InMemoryStore<CommandTarget>())
                .UseHandlerDiscovery();

            await configuration.CommandScheduler<CreateCommandTarget>().Schedule(new CreateCommandTarget("id"));

            await Clock.Current.Wait(1.Seconds());

            log.Should()
               .ContainSingle(e => e.Category == "CommandHandler<CreateCommandTarget>" &&
                                   e.OperationName == "Handle" &&
                                   e.Operation.IsStart);
            log.Should()
               .ContainSingle(e => e.Category == "CommandHandler<CreateCommandTarget>" &&
                                   e.OperationName == "Handle" &&
                                   e.Operation.IsEnd);
        }

        [Fact]
        public async Task Trace_enables_diagnostic_output_on_all_receivers_when_Receive_is_called()
        {
            configuration.TraceCommands();

            var handler = CommandHandler.Create<string>(delivery =>
            {
                Console.WriteLine("here i am");
            });

            await configuration.CommandScheduler<string>().Schedule("hi!");

            await configuration.CommandReceiver<string>().Receive(handler);

            log.Should()
               .ContainSingle(e => e.Category == "CommandReceiver<String>" &&
                                   e.OperationName == "Receive" &&
                                   e.Operation.IsStart);
            log.Should()
               .ContainSingle(e => e.Category == "CommandReceiver<String>" &&
                                   e.OperationName == "Receive" &&
                                   e.Operation.IsEnd);
        }

        [Fact]
        public async Task Trace_enables_diagnostic_output_on_all_receivers_when_a_message_is_received_after_Subscribe_is_called()
        {
            configuration.TraceCommands();

            var handler = CommandHandler.Create<string>(delivery =>
            {
                Console.WriteLine("here i am");
            });

            configuration.CommandReceiver<string>().Subscribe(handler);

            await configuration.CommandScheduler<string>().Schedule("hi!");

            await Clock.Current.Wait(1.Seconds());

            log.Should()
               .ContainSingle(e => e.Category == "CommandReceiver<String>" &&
                                   e.OperationName == "Receive" &&
                                   e.Operation.IsStart);
            log.Should()
               .ContainSingle(e => e.Category == "CommandReceiver<String>" &&
                                   e.OperationName == "Receive" &&
                                   e.Operation.IsEnd);
        }

        [Fact]
        public void Trace_enables_diagnostic_output_on_all_receivers_when_Subscribe_is_called()
        {
            configuration.TraceCommands();

            var handler = CommandHandler.Create<string>(delivery =>
            {
            });

            configuration.CommandReceiver<string>().Subscribe(handler);

            log.Should()
               .ContainSingle(e => e.Category == "CommandReceiver<String>" &&
                                   e.OperationName == "Subscribe" &&
                                   e.Operation.IsStart);
            log.Should()
               .ContainSingle(e => e.Category == "CommandReceiver<String>" &&
                                   e.OperationName == "Subscribe" &&
                                   e.Operation.IsEnd);
        }

        [Fact]
        public async Task CommandScheduler_Trace_publishes_delivery_properties_as_telemetry_properties()
        {
            // arrange
            var scheduler = CommandScheduler.Create<CreateCommandTarget>(c =>
            {
            }).Trace();

            var delivery = new CommandDelivery<CreateCommandTarget>(
                new CreateCommandTarget("the-id"),
                idempotencyToken: "the-idempotency-token",
                dueTime: DateTimeOffset.Parse("12/5/2086"));

            // act
            await scheduler.Schedule(delivery);

            // assert
            var logEvent = log[0];

            logEvent
                .Category
                .Should()
                .Be("CommandScheduler<CreateCommandTarget>",
                    "we're verifying that we have the right log event");

            var properties = logEvent
                .Evaluate()
                .Properties;

            properties
                .Should()
                .Contain(t => t.Name == "IdempotencyToken" &&
                              t.Value.As<string>() == "the-idempotency-token");
            properties
                .Should()
                .Contain(t => t.Name == "DueTime" &&
                              t.Value.As<DateTimeOffset>() == delivery.DueTime);
        }

        [Fact]
        public async Task CommandReceiver_Trace_publishes_delivery_properties_as_telemetry_properties()
        {
            // arrange
            var dueTime = DateTimeOffset.Parse("12/5/2086");

            var command = new CreateCommandTarget("the-id");

            var handler = CommandHandler.Create<CreateCommandTarget>(d => d.Complete());

            var delivery = new CommandDelivery<CreateCommandTarget>(
                command,
                idempotencyToken: "the-idempotency-token",
                dueTime: dueTime,
                numberOfPreviousAttempts: 4);
            await configuration.CommandScheduler<CreateCommandTarget>().Schedule(delivery);

            // act
            await configuration.CommandReceiver<CreateCommandTarget>().Receive(handler);

            // assert
            var logEvent = log[4];

            logEvent
                .Category
                .Should()
                .Be("CommandReceiver<CreateCommandTarget>",
                    "we're verifying that we have the right log event");

            var properties = logEvent
                .Evaluate()
                .Properties;

            properties
                .Should()
                .Contain(t => t.Name == "IdempotencyToken" &&
                              t.Value.As<string>() == "the-idempotency-token");
            properties
                .Should()
                .Contain(t => t.Name == "DueTime" &&
                              t.Value.As<DateTimeOffset>() == dueTime);

            properties
                .Should()
                .Contain(t => t.Name == "NumberOfPreviousAttempts" &&
                              t.Value.As<int>() == 4);
        }

        [Fact]
        public async Task CommandHandler_Trace_publishes_delivery_properties_as_telemetry_properties()
        {
            // arrange
            var dueTime = DateTimeOffset.Parse("12/5/2086");

            var handler = CommandHandler.Create<CreateCommandTarget>(d => d.Complete()).Trace();

            var command = new CreateCommandTarget("the-id");

            var delivery = new CommandDelivery<CreateCommandTarget>(
                command,
                idempotencyToken: "the-idempotency-token",
                dueTime: dueTime,
                numberOfPreviousAttempts: 4);

            // act
            await handler.Handle(delivery);

            // assert
            var logEvent = log[0];

            logEvent
                .Category
                .Should()
                .Be("CommandHandler<CreateCommandTarget>",
                    "we're verifying that we have the right log event");

            var properties = logEvent
                .Evaluate()
                .Properties;

            properties
                .Should()
                .Contain(t => t.Name == "IdempotencyToken" &&
                              t.Value.As<string>() == "the-idempotency-token");
            properties
                .Should()
                .Contain(t => t.Name == "DueTime" &&
                              t.Value.As<DateTimeOffset>() == dueTime);

            properties
                .Should()
                .Contain(t => t.Name == "NumberOfPreviousAttempts" &&
                              t.Value.As<int>() == 4);
        }
    }
}
