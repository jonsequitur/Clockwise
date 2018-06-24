using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Clockwise
{
    internal class ConcurrentSet<T> : IEnumerable<T>
    {
        private readonly ConcurrentDictionary<T, byte> dictionary = new ConcurrentDictionary<T, byte>();

        public bool TryAdd(T value) => dictionary.TryAdd(value, 0);

        public bool TryRemove(T value) => dictionary.TryRemove(value, out _);

        public bool Contains(T value) => dictionary.ContainsKey(value);
        public IEnumerator<T> GetEnumerator()
        {
            return dictionary.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}