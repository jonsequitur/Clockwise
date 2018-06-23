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

            Container.OnFailedResolve =
                (type, exception) =>
                    new ConfigurationException(
                        $"Clockwise can't create an instance of {type} unless you register it first via Configuration.UseDependency or Configuration.UseDependencies.", exception);
        }

        internal PocketContainer Container { get; }

        internal ConfigurationProperties Properties { get; } = new ConfigurationProperties();

        public ICommandReceiver<T> CommandReceiver<T>()
        {
            var receiver = Container.Resolve<ICommandReceiver<T>>();
            if (Properties.CircuitBreakerEnabled)
            {
                var circuitBreakerGenerator = Container.Resolve<Func<T, ICircuitBreaker>>();
                var circuitBreaker = circuitBreakerGenerator?.Invoke(default);

                if (circuitBreaker != null)
                {
                    receiver = receiver.UseMiddleware(receive: async (handle, timeout, next) =>
                        {
                            return await next(async delivery =>
                            {
                                if (circuitBreaker.StateDescriptor.State == CircuitBreakerState.Open)
                                    return delivery.Retry(circuitBreaker.StateDescriptor.TimeToLive);

                                var result = await handle(delivery);

                                switch (result)
                                {
                                    case PauseDeliveryResult<T> pause:
                                        circuitBreaker.SignalFailure(pause.PausePeriod);
                                        break;
                                }

                                return result;

                            }, timeout);
                        },
                        subscribe: (onNext, next) => next(onNext));
                }
            }

            return receiver;
        }

        public ICommandScheduler<T> CommandScheduler<T>()
        {
            var scheduler = Container.Resolve<ICommandScheduler<T>>();

            if (Properties.TracingEnabled)
            {
                scheduler = scheduler.Trace();
            }

            return scheduler;
        }

        public void RegisterForDisposal(IDisposable disposable) => disposables.Add(disposable);

        internal List<CommandHandlerDescription> CommandHandlerDescriptions { get; } = new List<CommandHandlerDescription>();

        public void Dispose() => disposables.Dispose();
    }
}
