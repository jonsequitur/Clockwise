namespace Clockwise
{
    public class CircuitBrakerSetState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBrakerSetState"/> class.
        /// </summary>
        /// <param name="targetState">State of the target.</param>
        public CircuitBrakerSetState(CircuitBreakerState targetState)
        {
            TargetState = targetState;
        }

        /// <summary>
        /// Gets the state to be set.
        /// </summary>
        /// <value>
        /// The state to be set on the circuit breaker.
        /// </value>
        public CircuitBreakerState TargetState { get;  }
    }

    public class CircuitBrakerSetState<T> : CircuitBrakerSetState
    {
        public CircuitBrakerSetState(CircuitBreakerState targetState) : base(targetState)
        {
        }
    }
}