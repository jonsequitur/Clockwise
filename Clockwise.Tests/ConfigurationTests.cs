using System;
using FluentAssertions;
using System.Threading.Tasks;
using Pocket;
using Xunit;

namespace Clockwise.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public async Task UseInMemoryScheduling_returns_receivers_and_schedulers_per_command_type_that_are_prewireed_to_each_other()
        {
            string received = null;

            using (var configuration = new Configuration().UseInMemoryScheduling())
            {
                var scheduler = configuration.CommandScheduler<string>();

                var receiver = configuration.CommandReceiver<string>();

                await scheduler.Schedule("hello");

                await receiver.Receive(delivery =>
                {
                    received = delivery.Command;
                    return delivery.Complete();
                });

                received.Should().Be("hello");
            }
        }

        [Fact]
        public void if_no_virtual_clock_is_started_then_it_will_start_one_on_demand()
        {
            using (var configuration = new Configuration())
            {
                // register and trigger creation of an InMemoryCommandBus
                configuration.UseInMemoryScheduling()
                             .CommandReceiver<string>();

                Clock.Current.Should().BeOfType<VirtualClock>();
            }
        }

        [Fact]
        public void UseDependency_can_be_used_to_override_default_implementations()
        {
            var configuration = new Configuration();

            configuration.UseDependency<ICommandReceiver<int>>(_ => new FakeReceiver<int>());

            configuration.CommandReceiver<int>()
                         .Should()
                         .BeOfType<FakeReceiver<int>>();
        }

        [Fact]
        public void UseDependencies_can_be_used_to_byo_container()
        {
            var configuration = new Configuration();
            var container = new PocketContainer()
                .RegisterGeneric(
                    variantsOf: typeof(ICommandReceiver<>),
                    to: typeof(FakeReceiver<>));

            configuration.UseDependencies(type => () => container.Resolve(type));

            configuration.CommandReceiver<int>()
                         .Should()
                         .BeOfType<FakeReceiver<int>>();
        }

        [Fact]
        public void Objects_can_be_registered_to_be_disposed_when_the_configuration_is_dispoed()
        {
            var wasDisposed = false;

            var configuration = new Configuration();

            configuration.RegisterForDisposal(Disposable.Create(() => wasDisposed = true));

            configuration.Dispose();

            wasDisposed.Should().BeTrue();
        }

        [Fact]
        public void RegisterForDisposal_can_be_used_to_specify_objects_that_should_be_disposed_when_the_Configuration_is_disposed()
        {
            var disposed = false;

            var disposable = Disposable.Create(() => disposed = true);

            var configuration = new Configuration();

            configuration.RegisterForDisposal(disposable);

            disposed.Should().BeFalse();

            configuration.Dispose();

            disposed.Should().BeTrue();
        }

        [Fact]
        public async Task Handlers_can_by_dynamically_wired_up_when_commands_are_scheduled()
        {
            var id = Guid.NewGuid().ToString();

            var store = new InMemoryStore<CommandTarget>();

            using (var configuration = new Configuration()
                .UseDependency<IStore<CommandTarget>>(_ => store)
                .UseInMemoryScheduling()
                .UseHandlerDiscovery())
            {
                var scheduler = configuration.CommandScheduler<CreateCommandTarget>();

                await scheduler.Schedule(new CreateCommandTarget(id));

                await Clock.Current.Wait(1.Seconds());
            }

            var commandTarget = await store.Get(id);

            commandTarget.Should().NotBeNull();
        }

        private class FakeReceiver<T> : ICommandReceiver<T>
        {
            public IDisposable Subscribe(Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> onNext) =>
                throw new NotImplementedException();

            public Task<ICommandDeliveryResult> Receive(Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> onNext, TimeSpan? timeout = null) =>
                throw new NotImplementedException();
        }
    }
}
