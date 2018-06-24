namespace Clockwise
{
    public interface IIdempotent
    {
        /// <summary>
        /// Gets the idempotency token.
        /// </summary>
        /// <value>
        /// The idempotency token.
        /// </value>
        string IdempotencyToken { get; }
    }
}