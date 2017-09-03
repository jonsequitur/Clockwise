using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandHandler<T>
    {
        Task<ICommandDeliveryResult> Handle(ICommandDelivery<T> delivery);
    }
}