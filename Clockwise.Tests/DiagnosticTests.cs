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
            disposables.Add(LogEvents.Subscribe(e => output.WriteLine(e.ToLogString())));
            disposables.Add(LogEvents.Subscribe(log.Add));

            configuration = new Configuration();
            disposables.Add(configuration.UseInMemoryScheduling());
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
    }
}
