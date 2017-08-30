using System;
using Pocket;
using Xunit.Abstractions;

namespace Clockwise.Tests
{
    public class InMemorySchedulingAndHandlingTests : SchedulingAndHandlingTests
    {
        private readonly VirtualClock virtualClock;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly PocketContainer container = new PocketContainer();

        public InMemorySchedulingAndHandlingTests(ITestOutputHelper output) : base(output)
        {
            virtualClock = VirtualClock.Start();

            container
                .Register(c => virtualClock)
                .RegisterGeneric(
                    variantsOf: typeof(ICommandBus<>),
                    to: typeof(InMemoryCommandBus<>),
                    singletons: true);

            disposables.Add(virtualClock);
        }

        protected override IClock Clock => virtualClock;

        protected override ICommandScheduler<T> CreateScheduler<T>() =>
            container.Resolve<ICommandBus<T>>();

        protected override ICommandReceiver<T> CreateReceiver<T>() =>
            container.Resolve<ICommandBus<T>>();

        protected override void SubscribeHandler<T>(Func<CommandDelivery<T>, CommandDeliveryResult<T>> handle) =>
            RegisterForDisposal(
                container.Resolve<ICommandBus<T>>()
                         .Subscribe(CreateHandler(handle)));

        protected override ICommandHandler<T> CreateHandler<T>(Func<CommandDelivery<T>, CommandDeliveryResult<T>> handle) =>
            CommandHandler
                .Create(handle)
                .RetryOnException()
                .Trace();

        public override void Dispose()
        {
            disposables.Dispose();
            base.Dispose();
        }
    }
}
