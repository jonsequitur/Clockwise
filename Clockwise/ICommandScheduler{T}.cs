using System.Threading.Tasks;

namespace Clockwise
{
    /// <summary>
    /// Command scheduler contract.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ICommandScheduler<in T>
    {
        /// <summary>
        /// Schedules the specified delivery.
        /// </summary>
        /// <param name="delivery">The delivery.</param>
        /// <returns></returns>
        Task Schedule(ICommandDelivery<T> delivery);
    }
}
