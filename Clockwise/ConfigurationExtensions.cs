using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public static class ConfigurationExtensions
    {
        public static Configuration OnSchedule<T>(
            this Configuration configuration,
            CommandSchedulingMiddleware<T> use)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            configuration.Container
                         .AfterCreating<ICommandScheduler<T>>(
                             scheduler => scheduler.UseMiddleware(use));

            return configuration;
        }

        public static Configuration OnHandle<T>(
            this Configuration configuration,
            CommandHandlingMiddleware<T> use)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            configuration.Container
                         .AfterCreating<ICommandHandler<T>>(
                             handler => handler.UseMiddleware(use));

            return configuration;
        }

        public static Configuration UseDependency<T>(
            this Configuration configuration,
            Func<Func<Type, object>, T> resolve)
        {
            configuration.Container
                         .Register(c => resolve(c.Resolve));
            return configuration;
        }

        public static Configuration UseCircuitbreaker<TChannel, TCircuitBreaker>(this Configuration configuration) where TCircuitBreaker : CircuitBreaker<TCircuitBreaker>
        {
            configuration.Container.AfterCreating<ICommandReceiver<TChannel>>(receiver =>
            {
                TCircuitBreaker cb;
                try
                {
                    cb = configuration.Container.Resolve<TCircuitBreaker>();
                    
                }
                catch (Exception e)
                {
                    throw new ConfigurationException($"Failure during creation of circuit breaker {typeof(TCircuitBreaker).Name}",e);
                }

                async Task<ICommandDeliveryResult> Receive(HandleCommand<TChannel> handlerDelegate, TimeSpan? timeout, Func<HandleCommand<TChannel>, TimeSpan?, Task<ICommandDeliveryResult>> next)
                {
                    return await next(async delivery =>
                    {
                        var result = await handlerDelegate(delivery);
                        return result;
                    }, timeout);
                }

                IDisposable Subscribe(HandleCommand<TChannel> handle, Func<HandleCommand<TChannel>, IDisposable> subscribe)
                {
                    return subscribe(async delivery =>
                    {
                        {
                            var stateDescriptor = await cb.GetLastStateAsync();
                            if (stateDescriptor.State == CircuitBreakerState.Open)
                            {

                                return delivery.Retry(stateDescriptor.TimeToLive);
                            }

                            var deliveryResult = await handle(delivery);
                            switch (deliveryResult)
                            {
                                case PauseDeliveryResult<TChannel> pause:
                                    await cb.SignalFailure(pause.PausePeriod);
                                    break;
                                default:
                                    await cb.SignalSuccess();
                                    break;
                            }

                            return deliveryResult;
                        }
                    });
                }

                var instrumented = receiver.UseMiddleware(
                    receive: Receive,
                    subscribe: Subscribe);

                return instrumented;
            });
            return configuration;
        }
        public static Configuration UseDependencies(
            this Configuration configuration,
            Func<Type, Func<object>> strategy)
        {
            configuration.Container
                         .AddStrategy(t =>
                         {
                             Func<object> resolveFunc = strategy(t);
                             if (resolveFunc != null)
                             {
                                 return container => resolveFunc();
                             }
                             return null;
                         });
            return configuration;
        }

        public static Configuration UseHandlerDiscovery(
            this Configuration configuration,
            IEnumerable<Assembly> withinAssemblies = null)
        {
            var commandHandlerDescriptions =
                (withinAssemblies?.Types() ??
                 Discover.ConcreteTypes())
                .SelectMany(
                    concreteType =>
                        concreteType.GetInterfaces()
                                    .Where(
                                        i => i.IsConstructedGenericType &&
                                             i.GetGenericTypeDefinition() == typeof(ICommandHandler<>))
                                    .Select(
                                        handlerInterface => new CommandHandlerDescription(handlerInterface, concreteType)))
                .ToArray();

            configuration.CommandHandlerDescriptions
                         .AddRange(commandHandlerDescriptions);

            foreach (var handlerDescription in commandHandlerDescriptions)
            {
                configuration.Container.Register(
                    handlerDescription.HandlerInterface,
                    c => c.Resolve(handlerDescription.ConcreteHandlerType));
            }

            return configuration;
        }

        public static Configuration UseInMemeoryCircuitBreakerStorage(this Configuration configuration)
        {
            configuration.Container.TryRegisterSingle<ICircuitBreakerStorage>(_ => new InMemoryCircuitBreakerStorage());

            return configuration;
        }
        public static Configuration UseInMemoryScheduling(
            this Configuration configuration)
        {
            // map of command type to bus instance
            var busesByType = new ConcurrentDictionary<Type, object>();

            configuration.Container
                         .AddStrategy(
                             SingleInMemoryCommandBusPerCommandType(
                                 configuration,
                                 busesByType));

            configuration.RegisterForDisposal(Disposable.Create(() =>
            {
                foreach (var bus in busesByType.Values.Cast<IDisposable>())
                {
                    bus.Dispose();
                }
            }));

            return configuration;
        }

        public static Configuration TraceCommands(this Configuration configuration)
        {
            configuration.Properties.TracingEnabled = true;

            return configuration;
        }

        private static Func<Type, Func<PocketContainer, object>> SingleInMemoryCommandBusPerCommandType(
            Configuration configuration,
            ConcurrentDictionary<Type, object> busesByType) =>
            type =>
            {
                if (type.IsSchedulerOrReceiver())
                {
                    var commandType = type.GenericTypeArguments[0];
                    var commandBusType = typeof(InMemoryCommandBus<>).MakeGenericType(commandType);
                    var receiverType = typeof(ICommandReceiver<>).MakeGenericType(commandType);
                    var schedulerType = typeof(ICommandScheduler<>).MakeGenericType(commandType);

                    configuration.Container
                                 .RegisterSingle(commandBusType,
                                                 c => Activator.CreateInstance(
                                                     commandBusType,
                                                     c.Resolve<VirtualClock>()))
                                 .RegisterSingle(receiverType, CreateAndSubscribeDiscoveredHandlers)
                                 .RegisterSingle(schedulerType, CreateAndSubscribeDiscoveredHandlers);

                    return c => c.Resolve(type);

                    object CreateAndSubscribeDiscoveredHandlers(PocketContainer container) =>
                        busesByType.GetOrAdd(commandType, _ =>
                        {
                            var bus = container.Resolve(commandBusType);

                            TrySubscribeDiscoveredHandler(
                                configuration,
                                commandType,
                                container,
                                (dynamic)bus);

                            return bus;
                        });
                }

                return null;
            };

        private static bool IsSchedulerOrReceiver(this Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }

            var genericTypeDefinition = type.GetGenericTypeDefinition();

            return genericTypeDefinition == typeof(ICommandScheduler<>) ||
                   genericTypeDefinition == typeof(ICommandReceiver<>);
        }

        internal static void TrySubscribeDiscoveredHandler<T>(
            Configuration configuration,
            Type commandType,
            PocketContainer c,
            ICommandReceiver<T> receiver)
        {
            foreach (var handlerDescription in
                configuration.CommandHandlerDescriptions
                             .Where(t => t.HandledCommandType == commandType))
            {
                var handler = c.Resolve(handlerDescription.HandlerInterface) as ICommandHandler<T>;

                if (configuration.Properties.TracingEnabled)
                {
                    handler = handler.Trace();
                }

                var subscription = receiver.Subscribe(handler);

                configuration.RegisterForDisposal(subscription);

                Logger<Configuration>.Log.Trace(
                    "Subscribing discovered command handler: {handler} to handle commands of type {commandType}",
                    handlerDescription.ConcreteHandlerType,
                    handlerDescription.HandledCommandType);
            }
        }
    }
}
