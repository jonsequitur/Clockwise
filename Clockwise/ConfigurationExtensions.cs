using System;
using System.Collections.Concurrent;
using System.Linq;
using Pocket;
using static Pocket.Logger<Clockwise.Configuration>;

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
            this Configuration configuration)
        {
            var commandHandlerDescriptions =
                Discover.ConcreteTypes()
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
                                (dynamic) bus);

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

                Log.Trace(
                    "Subscribing discovered command handler: {handler} to handle commands of type {commandType}",
                    handlerDescription.ConcreteHandlerType,
                    handlerDescription.HandledCommandType);
            }
        }
    }
}
