using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreaker : IDisposable
    {
        Task<CircuitBreakerStateDescriptor> GetLastStateAsync();
        Task SignalSuccess();
        Task SignalFailure(TimeSpan expiry);
    }

    public abstract class CircuitBreaker<T>: IObserver<CircuitBreakerStateDescriptor>, ICircuitBreaker
    where T : CircuitBreaker<T>
    {
        private readonly ICircuitBreakerStorage storage;
        private IDisposable setStateSubscription;
        private IDisposable storageSubscription;

        /// <summary>
        /// Initializes a new instance of the <see /> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        /// <exception cref="ArgumentNullException">storage</exception>
        protected CircuitBreaker(ICircuitBreakerStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            storageSubscription = this.storage.Subscribe<T>(this);
        }

        private CircuitBreakerStateDescriptor stateDescriptor;

     
       
        public Task<CircuitBreakerStateDescriptor> GetLastStateAsync()
        {
            return stateDescriptor == null ? storage.GetLastStateOfAsync<T>() : Task.FromResult(stateDescriptor);
        }

        public async Task SignalSuccess()
        {
            await storage.SignalSuccessForAsync<T>();
        }

        public async Task SignalFailure(TimeSpan expiry)
        {
            await storage.SignalFailureForAsync<T>(expiry);
        }

        public void Dispose()
        {
            setStateSubscription?.Dispose();
            setStateSubscription = null;
            storageSubscription?.Dispose();
            storageSubscription = null;

        }

        void IObserver<CircuitBreakerStateDescriptor>.OnCompleted()
        {
        }

        void IObserver<CircuitBreakerStateDescriptor>.OnError(Exception error)
        {
        }

        void IObserver<CircuitBreakerStateDescriptor>.OnNext(CircuitBreakerStateDescriptor value)
        {
            stateDescriptor = value;
        }
    }
}
