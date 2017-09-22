using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandHandler<in T>
    {
        Task<ICommandDeliveryResult> Handle(ICommandDelivery<T> delivery);
    }
}