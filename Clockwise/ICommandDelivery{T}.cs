using System;

namespace Clockwise
{
    public interface ICommandDelivery<out T> : ICommandDelivery
    {
        T Command { get; }
    }
}
