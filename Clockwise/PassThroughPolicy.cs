using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public class PassThroughPolicy<TChannel> : HalfOpenStatePolicy<TChannel>
    {
        public override Task<ICommandDeliveryResult> Handle(HandleCommand<TChannel> handlerDelegate, ICommandDelivery<TChannel> delivery)
        {
            if (handlerDelegate == null) throw new ArgumentNullException(nameof(handlerDelegate));
            if (delivery == null) throw new ArgumentNullException(nameof(delivery));
            return handlerDelegate(delivery);
        }
    }
}