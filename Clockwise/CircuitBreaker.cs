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

    public sealed class CircuitBraker : ICircuitBreaker, IDisposable, IObserver<CircuitBreakerStateDescriptor>
    {
        private readonly ICircuitBreakerStorage _storage;
        private readonly ICommandScheduler<CircuitBrakerSetState> _scheduler;
        private IDisposable _setStateSubscription;
        private IDisposable storageSubscription;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBraker"/> class.
        /// </summary>
        /// <param name="storage">The backing storage.</param>
        /// <param name="pocketConfiguration">The pocket configuration.</param>
        /// <exception cref="ArgumentNullException">storage</exception>
        public CircuitBraker(ICircuitBreakerStorage storage, Configuration pocketConfiguration = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            StateDescriptor = _storage.GetStateAsync().Result;
            storageSubscription = _storage.Subscribe(this);

            if (pocketConfiguration != null)
            {
                _scheduler = pocketConfiguration.CommandScheduler<CircuitBrakerSetState>();
                _setStateSubscription = pocketConfiguration.CommandReceiver<CircuitBrakerSetState>().Subscribe(async (delivery) =>
                {
                    await _storage.SetStateAsync(delivery.Command.TargetState);
                    return delivery.Complete();
                });
            }
        }
        public CircuitBreakerStateDescriptor StateDescriptor { get; private set; }
        private async Task SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            await _storage.SetStateAsync(newState, expiry);
            if (_scheduler != null && newState == CircuitBreakerState.Open && expiry?.TotalSeconds > 0)
            {
                await _scheduler.Schedule(new CircuitBrakerSetState(CircuitBreakerState.Closed), expiry.Value);
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
            _setStateSubscription?.Dispose();
            _setStateSubscription = null;
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
