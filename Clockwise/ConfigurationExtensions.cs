using System;
using System.Collections.Concurrent;
using System.Linq;
using Pocket;

namespace Clockwise
{
    public static class ConfigurationExtensions
    {
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
            configuration.CommandHandlerDescriptions
                         .AddRange(Discover.ConcreteTypes()
                                           .ImplementingOpenGenericInterfaces(typeof(ICommandHandler<>))
                                           .Select(t => new CommandHandlerDescription(t))
                                           .ToList());

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
                if (type.IsGenericType &&
                    IsSchedulerOrReceiver(type))
                {
                    var commandType = type.GenericTypeArguments[0];
                    var commandBusType = typeof(InMemoryCommandBus<>).MakeGenericType(commandType);

                    return container =>
                        busesByType.GetOrAdd(
                            commandType,
                            _ =>
                            {
                                var bus = container.Resolve(commandBusType);

                                TrySubscribeDiscoveredHandler(
                                    configuration,
                                    commandType,
                                    container,
                                    bus);

                                return bus;
                            });
                }

                return null;
            };

        private static bool IsSchedulerOrReceiver(Type type)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();

            return genericTypeDefinition == typeof(ICommandScheduler<>) ||
                   genericTypeDefinition == typeof(ICommandReceiver<>);
        }

        private static void TrySubscribeDiscoveredHandler(
            Configuration configuration,
            Type commandType,
            PocketContainer c,
            dynamic receiver)
        {
            foreach (var handlerDescription in configuration.CommandHandlerDescriptions.Where(t => t.HandledCommandTypes.Contains(commandType)))
            {
                var handler = c.Resolve(handlerDescription.ConcreteHandlerType);

                if (configuration.Properties.TracingEnabled)
                {
                    handler = CommandHandler.Trace((dynamic) handler);
                }

                var subscription = CommandReceiver.Subscribe(
                    (dynamic) receiver,
                    (dynamic) handler);

                configuration.RegisterForDisposal(subscription);
            }
        }
    }
}
