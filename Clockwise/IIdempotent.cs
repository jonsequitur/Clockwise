namespace Clockwise
{
    public interface IIdempotent
    {
        string IdempotencyToken { get; }
    }
}