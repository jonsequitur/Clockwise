namespace Clockwise
{
    public enum CircuitBreakerState
    {
        /// <summary>
        /// Closed state meansthe circuit is available for work
        /// </summary>
        Closed = 0,
        /// <summary>
        /// Open state means the circuit is unavailable for work
        /// </summary>
        Open,
        /// <summary>
        /// HalfOpen state means that the circuit is partially able to work.
        /// Tshis could mean that the underlying resource is partially recovered.
        /// </summary>
        HalfOpen
    }
}