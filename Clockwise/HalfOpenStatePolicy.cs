using System.Threading.Tasks;

namespace Clockwise
{
    public abstract class HalfOpenStatePolicy<TChannel>
    {
        public abstract Task<ICommandDeliveryResult> Handle(HandleCommand<TChannel> handlerDelegate, ICommandDelivery<TChannel> delivery);
    }
}