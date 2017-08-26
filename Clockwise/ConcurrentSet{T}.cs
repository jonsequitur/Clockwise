using System.Collections.Concurrent;

namespace Clockwise
{
    internal class ConcurrentSet<T>
    {
        private readonly ConcurrentDictionary<T, byte> dictionary = new ConcurrentDictionary<T, byte>();

        public bool TryAdd(T value) => dictionary.TryAdd(value, 0);

        public bool Contains(T value) => dictionary.ContainsKey(value);
    }
}