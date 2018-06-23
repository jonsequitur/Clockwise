using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreakerStateMachine
    {
        /// <summary>
        /// Called to notify success.
        /// <remarks>Invoking this method will cause a transition to <see cref="CircuitBreakerState.HalfOpen"/> state
        /// if the current state is <see cref="CircuitBreakerState.Open"/>,
        /// otherwise will transition to <see cref="CircuitBreakerState.Closed"/> state.</remarks>
        /// </summary>
        void SignalSuccess();
        /// <summary>
        /// Called to notify failure.
        /// <remarks>Invoking this method will cause a transition to <see cref="CircuitBreakerState.Open"/> state.</remarks>
        /// </summary>
        void SignalFailure(TimeSpan? expiry = null);
    }

    public interface ICircuitBreaker : ICircuitBreakerStateMachine
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
    }

    public sealed class CircuitBraker : ICircuitBreaker, IDisposable, IObserver<CircuitBreakerStateDescriptor>
    {
        private readonly ICircuitBreakerStorage storage;
        private readonly ICommandScheduler<CircuitBrakerSetState> scheduler;
        private IDisposable setStateSubscription;
        private IDisposable storageSubscription;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBraker"/> class.
        /// </summary>
        /// <param name="circuitBreakerStorage">The backing circuitBreakerStorage.</param>
        /// <param name="pocketConfiguration">The pocket configuration.</param>
        /// <exception cref="ArgumentNullException">circuitBreakerStorage</exception>
        public CircuitBraker(ICircuitBreakerStorage circuitBreakerStorage, Configuration pocketConfiguration = null)
        {
            storage = circuitBreakerStorage ?? throw new ArgumentNullException(nameof(circuitBreakerStorage));
            StateDescriptor = storage.GetStateAsync().Result;
            storageSubscription = storage.Subscribe(this);

            if (pocketConfiguration != null)
            {
                scheduler = pocketConfiguration.CommandScheduler<CircuitBrakerSetState>();
                setStateSubscription = pocketConfiguration.CommandReceiver<CircuitBrakerSetState>().Subscribe(async (delivery) =>
                {
                    await storage.SetStateAsync(delivery.Command.TargetState);
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
                await scheduler.Schedule(new CircuitBrakerSetState(CircuitBreakerState.Closed), expiry.Value);
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

        public void SignalSuccess()
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

        public void SignalFailure(TimeSpan? expiry = null)
        {
            switch (StateDescriptor.State)
            {
                case CircuitBreakerState.Closed:
                    Open(expiry);
                    break;
                case CircuitBreakerState.HalfOpen:
                    Open(expiry);
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
