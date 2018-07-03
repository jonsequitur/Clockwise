using System.Threading.Tasks;
using FluentAssertions.Extensions;

namespace Clockwise.Tests
{
    public class TestCommandHandler : ICommandHandler<TestCommand>
    {
        public async  Task<ICommandDeliveryResult> Handle(ICommandDelivery<TestCommand> delivery)
        {
            await Task.Yield();
            if (delivery.Command.Payload > 10)
            {
                return delivery.PauseAllDeliveriesFor(5.Seconds());
            }
            delivery.Command.Processed.Add(delivery.Command.Payload);
            return delivery.Complete();
        }
    }
}