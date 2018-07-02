using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class HalfOpenStatePolicy<T>
    {
        public abstract Task<ICommandDeliveryResult> Handle(HandleCommand<T> handlerDelegate, ICommandDelivery<T> delivery);
    }
}