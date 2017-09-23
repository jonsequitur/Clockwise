using System;
using System.Collections.Generic;
using Pocket;

namespace Clockwise
{
    public class Configuration : IDisposable
    {
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public Configuration()
        {
            Container = new PocketContainer()
                .Register(c => Clock.Current)
                .Register(c => Clock.Current as VirtualClock ??
                               VirtualClock.Start());
        }

        internal PocketContainer Container { get; }

        public ICommandReceiver<T> CommandReceiver<T>() =>
            Container.Resolve<ICommandReceiver<T>>();

        public ICommandScheduler<T> CommandScheduler<T>() =>
            Container.Resolve<ICommandScheduler<T>>();

        public void RegisterForDisposal(IDisposable disposable) => disposables.Add(disposable);

        internal List<CommandHandlerDescription> CommandHandlerDescriptions { get; } = new List<CommandHandlerDescription>();

        public void Dispose() => disposables.Dispose();
    }
}
