using System;
using System.Linq;

namespace Clockwise
{
    internal class CommandHandlerDescription
    {
        public CommandHandlerDescription(Type handlerInterface, Type concreteType)
        {
            HandlerInterface = handlerInterface ??
                               throw new ArgumentNullException(nameof(handlerInterface));

            ConcreteHandlerType = concreteType ??
                                  throw new ArgumentNullException(nameof(concreteType));

            HandledCommandType = handlerInterface.GetGenericArguments().Single();
        }

        public Type HandledCommandType { get; }

        public Type HandlerInterface { get; }

        public Type ConcreteHandlerType { get; }
    }
}
