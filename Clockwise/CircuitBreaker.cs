using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreaker
    {
        CircuitBreakerStateDescriptor StateDescriptor { get; }
        /// <summary>
        /// Transition the circuit breaker to <see cref="CircuitBreakerState.Open"/> state.
        /// </summary>
        /// <param name="expiry">The expiry timespan for the <see cref="CircuitBreakerState.Open"/> state.
        /// If specified the circuit breaker will transition back to <see cref="CircuitBreakerState.Closed"/> state after the expiration period elapsed.
        /// </param>
        void Open(TimeSpan? expiry = null);
        void HalfOpen();
        /// <summary>
        ///Transition the circuit breaker to <see cref="CircuitBreakerState.Closed"/> state.
        /// </summary>
        void Close();
        /// <summary>
        /// Called to notify success.
        /// <remarks>Invoking this method will cause a transition to <see cref="CircuitBreakerState.HalfOpen"/> state
        /// if the current state is <see cref="CircuitBreakerState.Open"/>,
        /// otherwise will transition to <see cref="CircuitBreakerState.Closed"/> state.</remarks>
        /// </summary>
        void OnSuccess();
        /// <summary>
        /// Called to notify failure.
        /// <remarks>Invoking this method will cause a transition to <see cref="CircuitBreakerState.Open"/> state.</remarks>
        /// </summary>
        void OnFailure();
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
                _setStateSubscription = pocketConfiguration.CommandReceiver<CircuitBrakerSetState>().Subscribe((delivery) =>
                {
                    _storage.SetState(delivery.Command.TargetState);
                    return Task.FromResult(new CompleteDeliveryResult<CircuitBrakerSetState>(delivery) as ICommandDeliveryResult);
                });
            }
        }
        public CircuitBreakerStateDescriptor StateDescriptor { get; private set; }
        private void SetState(CircuitBreakerState newState, TimeSpan? expiry = null)
        {
            _storage.SetState(newState, expiry);
            if (_scheduler != null && newState == CircuitBreakerState.Open && expiry?.TotalSeconds > 0)
            {
                _scheduler.Schedule(new CircuitBrakerSetState(CircuitBreakerState.Closed), expiry.Value);
            }
        }

        public void Open(TimeSpan? expiry = null)
        {
            if (StateDescriptor.State != CircuitBreakerState.Open)
            {
                SetState(CircuitBreakerState.Open, expiry);
            }
        }

        public void HalfOpen()
        {
            if (StateDescriptor.State == CircuitBreakerState.Open)
            {
                SetState(CircuitBreakerState.HalfOpen);
            }
            else
            {
                throw new InvalidOperationException("Can transition to half open only from open state");
            }
        }

        public void Close()
        {
            if (StateDescriptor.State != CircuitBreakerState.Closed)
            {
                SetState(CircuitBreakerState.Closed);
            }
        }

        public void OnSuccess()
        {
            switch (StateDescriptor.State)
            {
                case CircuitBreakerState.Open:
                    HalfOpen();
                    break;
                case CircuitBreakerState.HalfOpen:
                    Close();
                    break;
            }
        }

        public void OnFailure()
        {
            switch (StateDescriptor.State)
            {
                case CircuitBreakerState.Closed:
                    Open();
                    break;
                case CircuitBreakerState.HalfOpen:
                    Open();
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
