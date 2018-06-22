using System;
using System.Threading.Tasks;

namespace Clockwise
{
    public interface ICircuitBreaker
    {
        CircuitBreakerStateDescriptor StateDescriptor { get; }
        void Open(TimeSpan? expiry = null);
        void HalfOpen();
        void Close();
        void OnSuccess();
        void OnFailure();
    }



    public sealed class CircuitBraker : ICircuitBreaker
    {
        private readonly ICircuitBreakerStorage _storage;
        private readonly ICommandScheduler<CircuitBrakerSetState> _scheduler;
        private IDisposable _setStateSubscription;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBraker"/> class.
        /// </summary>
        /// <param name="storage">The backing storage.</param>
        /// <param name="pocketConfiguration">The pocket configuration.</param>
        /// <exception cref="ArgumentNullException">storage</exception>
        public CircuitBraker(ICircuitBreakerStorage storage, Configuration pocketConfiguration  = null )
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            StateDescriptor = _storage.GetStateAsync().Result;
            _storage.CircuitBreakerStateChanged += StorageOnCircuitBreakerStateChanged;

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

        private void StorageOnCircuitBreakerStateChanged(object sender, CircuitBreakerStateDescriptor e)
        {
            StateDescriptor = e;
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
    }
}
