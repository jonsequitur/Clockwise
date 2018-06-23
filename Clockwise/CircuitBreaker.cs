using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreaker
    {
        CircuitBreakerStateDescriptor StateDescriptor { get; }
        /// <summary>
        /// Called to notify success.
        /// <remarks>Invoking this method will cause a transition to <see cref="CircuitBreakerState.HalfOpen"/> state
        /// if the current state is <see cref="CircuitBreakerState.Open"/>,
        /// otherwise will transition to <see cref="CircuitBreakerState.Closed"/> state.</remarks>
        /// </summary>
        Task SignalSuccess();
        /// <summary>
        /// Called to notify failure.
        /// <remarks>Invoking this method will cause a transition to <see cref="CircuitBreakerState.Open" /> state.</remarks>
        /// </summary>
        /// <param name="expiry">The expiry.</param>
        Task SignalFailure(TimeSpan? expiry = null);
    }

    public sealed class CircuitBraker<T> : ICircuitBreaker, IDisposable, IObserver<CircuitBreakerStateDescriptor>
    {
        private readonly ICircuitBreakerStorage storage;
        private readonly ICommandScheduler<CircuitBrakerSetState<T>> scheduler;
        private IDisposable setStateSubscription;
        private IDisposable storageSubscription;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBraker{T}"/> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        /// <param name="pocketConfiguration">The pocket configuration.</param>
        /// <exception cref="ArgumentNullException">storage</exception>
        public CircuitBraker(ICircuitBreakerStorage storage, Configuration pocketConfiguration = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            StateDescriptor = this.storage.GetStateAsync().Result;
            storageSubscription = this.storage.Subscribe(this);

            if (pocketConfiguration != null)
            {
                scheduler = pocketConfiguration.CommandScheduler<CircuitBrakerSetState<T>>();
                setStateSubscription = pocketConfiguration.CommandReceiver<CircuitBrakerSetState<T>>().Subscribe(async (delivery) =>
                {
                    await this.storage.SetStateAsync(delivery.Command.TargetState);
                    return delivery.Complete();
                });
            }
        }
        public CircuitBreakerStateDescriptor StateDescriptor { get; private set; }
        private async Task SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            await storage.SetStateAsync(newState, expiry);
            if (scheduler != null && newState == CircuitBreakerState.Open && expiry?.TotalSeconds > 0)
            {
                await scheduler.Schedule(new CircuitBrakerSetState<T>(CircuitBreakerState.Closed), expiry.Value);
            }
        }

        public async Task Open(TimeSpan? expiry = null)
        {
            if (StateDescriptor.State != CircuitBreakerState.Open)
            {
                await SetState(CircuitBreakerState.Open, expiry);
            }
        }

        public async Task HalfOpen()
        {
            if (StateDescriptor.State == CircuitBreakerState.Open)
            {
                await SetState(CircuitBreakerState.HalfOpen);
            }
            else
            {
                throw new InvalidOperationException("Can transition to half open only from open state");
            }
        }

        public async Task Close()
        {
            if (StateDescriptor.State != CircuitBreakerState.Closed)
            {
                await SetState(CircuitBreakerState.Closed);
            }
        }

        public async Task SignalSuccess()
        {
            switch (StateDescriptor.State)
            {
                case CircuitBreakerState.Open:
                    await HalfOpen();
                    break;
                case CircuitBreakerState.HalfOpen:
                    await Close();
                    break;
            }
        }

        public async Task SignalFailure(TimeSpan? expiry = null)
        {
            switch (StateDescriptor.State)
            {
                case CircuitBreakerState.Closed:
                    await Open(expiry);
                    break;
                case CircuitBreakerState.HalfOpen:
                    await Open(expiry);
                    break;
            }
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
            StateDescriptor = value;
        }
    }
}
