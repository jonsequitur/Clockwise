using System;
using System.Collections.Generic;
using FluentAssertions;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
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
        public void UseInMemoryScheduling_registers_singleton_command_receiver_instances_per_command_type()
        {
            using (var configuration = new Configuration().UseInMemoryScheduling())
            {
                var receiver1 = configuration.CommandReceiver<int>();

                var receiver2 = configuration.CommandReceiver<int>();

                receiver1.Should().BeSameAs(receiver2);
            }
        }

        [Fact]
        public void UseInMemoryScheduling_registers_singleton_command_scheduler_instances_per_command_type()
        {
            using (var configuration = new Configuration().UseInMemoryScheduling())
            {
                var scheduler1 = configuration.CommandScheduler<int>();

                var scheduler2 = configuration.CommandScheduler<int>();

                scheduler1.Should().BeSameAs(scheduler2);
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
        public async Task Handlers_can_be_discovered_and_dynamically_wired_up_when_commands_are_scheduled()
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

        [Fact]
        public async Task OnSchedule_can_be_used_to_configure_scheduler_middleware_for_a_specific_command_type()
        {
            var scheduled = new List<string>();

            using (var configuration = new Configuration()
                .UseInMemoryScheduling())
            {
                configuration.OnSchedule<string>(async (delivery, next) =>
                {
                    scheduled.Add(delivery.Command);
                    await next(delivery);
                });

                using (var clock = VirtualClock.Start())
                {
                    clock.Repeat(async c =>
                    {
                        await configuration.CommandScheduler<string>().Schedule(c.Now().ToString());
                    }, () => 1.Seconds());

                    var handler = CommandHandler.Create<string>(delivery =>
                    {
                    });

                    configuration.CommandReceiver<string>().Subscribe(handler);

                    await Clock.Current.Wait(1.Minutes());
                }
            }

            scheduled.Count.Should().Be(60);
        }

        [Fact]
        public async Task OnHandle_can_be_used_to_configure_scheduler_middleware_for_a_specific_command_type()
        {
            var handled = new List<CreateCommandTarget>();

            using (var configuration = new Configuration()
                .UseHandlerDiscovery()
                .UseDependency<IStore<CommandTarget>>(_ => new InMemoryStore<CommandTarget>())
                .UseInMemoryScheduling())
            {
                configuration.OnHandle<CreateCommandTarget>(async (delivery, next) =>
                {
                    handled.Add(delivery.Command);
                    return await next(delivery);
                });

                using (var clock = VirtualClock.Start())
                {
                    clock.Repeat(async c =>
                    {
                        await configuration.CommandScheduler<CreateCommandTarget>()
                                           .Schedule(new CreateCommandTarget(Guid.NewGuid().ToString()));
                    }, () => 1.Seconds());

                    // an extra tick is required to get to 60 because the 60th command is dispatched at the 1 minute mark
                    await Clock.Current.Wait(1.Minutes() + 1.Ticks());
                }
            }

            handled.Count.Should().Be(60);
        }

        private class FakeReceiver<T> : ICommandReceiver<T>
        {
            public IDisposable Subscribe(CommandHandler<T> handle) =>
                throw new NotImplementedException();

            public Task<ICommandDeliveryResult> Receive(CommandHandler<T> handle, TimeSpan? timeout = null) =>
                throw new NotImplementedException();
        }
    }
}
