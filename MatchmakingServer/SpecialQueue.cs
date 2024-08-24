using System.Collections;

namespace MatchmakingServer
{
    public class SpecialQueue<T> : ICollection<T>
    {
        private readonly LinkedList<T> _list = [];

        public void Enqueue(T t)
        {
            _list.AddLast(t);
        }

        public T? Dequeue()
        {
            var node = _list.First;
            if (node == null)
            {
                return default;
            }

            _list.RemoveFirst();

            return node.Value;
        }

        public T? Peek()
        {
            var node = _list.First;
            if (node == null)
            {
                return default;
            }

            return node.Value;
        }

        public bool Remove(T t)
        {
            return _list.Remove(t);
        }

        public void Add(T item)
        {
            ((ICollection<T>)_list).Add(item);
        }

        public void Clear()
        {
            ((ICollection<T>)_list).Clear();
        }

        public bool Contains(T item)
        {
            return ((ICollection<T>)_list).Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            ((ICollection<T>)_list).CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)_list).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_list).GetEnumerator();
        }

        public int Count { get { return _list.Count; } }

        public bool IsReadOnly => ((ICollection<T>)_list).IsReadOnly;
    }
}
