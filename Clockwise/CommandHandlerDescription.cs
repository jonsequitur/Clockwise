using System;
using System.Collections.Generic;
using System.Linq;

namespace Clockwise
{
    internal class CommandHandlerDescription
    {
        public CommandHandlerDescription(Type type)
        {
            ConcreteHandlerType = type ??
                                  throw new ArgumentNullException(nameof(type));

            HandledCommandTypes = type.GetInterfaces()
                                      .Where(i => i.IsGenericType &&
                                                  i.GetGenericTypeDefinition() == typeof(ICommandHandler<>))
                                      .Select(i => i.GetGenericArguments().Single())
                                      .ToList();
        }

        public List<Type> HandledCommandTypes { get; }

        public Type ConcreteHandlerType { get; }
    }
}
