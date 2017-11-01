using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FluentAssertions;
using Pocket;
using Xunit;
using Xunit.Abstractions;

namespace Clockwise.Tests
{
    public class DiagnosticTests : IDisposable
    {
        private readonly Configuration configuration;
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private readonly ConcurrentQueue<string> log = new ConcurrentQueue<string>();

        public DiagnosticTests(ITestOutputHelper output)
        {
            disposables.Add(LogEvents.Subscribe(e => output.WriteLine(e.ToLogString())));
            disposables.Add(LogEvents.Subscribe(e => log.Enqueue(e.ToLogString())));

            configuration = new Configuration();
            disposables.Add(configuration.UseInMemoryScheduling());
        }

        public void Dispose() => disposables.Dispose();

        [Fact]
        public async Task Trace_enables_diagnostic_output_on_all_schedulers()
        {
            configuration.TraceCommands();

            var scheduler = configuration.CommandScheduler<string>();

            await scheduler.Schedule(new CommandDelivery<string>(
                                         "hi!",
                                         dueTime: DateTimeOffset.Parse("1/31/2017 12:05am +00:00"),
                                         idempotencyToken: "idempotency-token"));

            log.Should().Contain(e => e.Contains("[CommandScheduler<String>] [Schedule]  ▶ hi! (idempotency-token) due 1/31/2017 12:05:00 AM +00:00"));
            log.Should().Contain(e => e.Contains("[CommandScheduler<String>] [Schedule]  ⏹"));
        }

        [Fact]
        public async Task Trace_enables_diagnostic_output_on_discovered_handlers()
        {
            configuration
                .TraceCommands()
                .UseHandlerDiscovery();

            await configuration.CommandScheduler<string>().Schedule("hi!");

            await Clock.Current.Wait(1.Seconds());

            log.Should().Contain(e => e.Contains("[CommandHandler<String>] [Handle]  ▶"));
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
               .Contain(e => e.Contains("[CommandReceiver<String>] [Receive]  ⏹") &&
                             e.Contains("hi!"));
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

            log.Should().Contain(e => e.Contains("[CommandReceiver<String>] [Receive]  ▶") &&
                                      e.Contains("hi!"));
        }

        [Fact]
        public void Trace_enables_diagnostic_output_on_all_receivers_when_Subscribe_is_called()
        {
            configuration.TraceCommands();

            var handler = CommandHandler.Create<string>(delivery =>
            {
            });

            configuration.CommandReceiver<string>().Subscribe(handler);

            log.Should().Contain(e => e.Contains("[CommandReceiver<String>] [Subscribe]  ▶"));
        }
    }
}
