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
                .RegisterSingle(c => new InMemoryCommandBus<string>(virtualClock))
                .RegisterGeneric(
                    variantsOf: typeof(ICommandReceiver<>),
                    to: typeof(InMemoryCommandBus<>),
                    singletons: true)
                .RegisterGeneric(
                    variantsOf: typeof(ICommandScheduler<>),
                    to: typeof(InMemoryCommandBus<>),
                    singletons: true);

            disposables.Add(virtualClock);
        }

        protected override IClock Clock => virtualClock;

        protected override ICommandReceiver<T> CreateReceiver<T>() =>
            container.Resolve<ICommandReceiver<T>>();

        protected override ICommandScheduler<T> CreateScheduler<T>() =>
            container.Resolve<ICommandScheduler<T>>();

        protected override void SubscribeHandler<T>(Func<ICommandDelivery<T>, ICommandDeliveryResult> handle) =>
            RegisterForDisposal(
                CreateReceiver<T>()
                    .Subscribe(CreateHandler(handle)));

        protected override ICommandHandler<T> CreateHandler<T>(Func<ICommandDelivery<T>, ICommandDeliveryResult> handle) =>
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
