using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICommandHandler<T>
    {
        Task<CommandDeliveryResult<T>> Handle(ICommandDelivery<T> delivery);
    }
}