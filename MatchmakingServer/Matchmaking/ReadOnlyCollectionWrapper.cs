using System.Collections;

namespace MatchmakingServer
{
    public class ReadOnlyCollectionWrapper<T> : IReadOnlyCollection<T>
    {
        private readonly ICollection<T> _collection;
        public ReadOnlyCollectionWrapper(ICollection<T> collection)
        {
            _collection = collection;
        }

        public int Count
        {
            get { return _collection.Count; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _collection.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
    }
}
