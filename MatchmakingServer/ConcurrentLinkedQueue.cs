using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace MatchmakingServer
{
    public class ConcurrentLinkedQueue<T> : IReadOnlyCollection<T>
    {
        private readonly LinkedList<T> _list = new();
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly bool _cacheEnumerationSnapshot;
        private List<T>? _snapshot = null;
        private bool _snapshotStale = true;

        public ConcurrentLinkedQueue(bool cacheEnumerationSnapshot = true)
        {
            _cacheEnumerationSnapshot = cacheEnumerationSnapshot;
        }
        
        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _list.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public void Enqueue(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                _list.AddLast(item);
                _snapshotStale = true; // Invalidate the snapshot
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public T? Dequeue()
        {
            _lock.EnterWriteLock();
            try
            {
                if (_list.Count == 0)
                {
                    return default;
                }

                var value = _list.First!.Value;
                _list.RemoveFirst();
                _snapshotStale = true; // Invalidate the snapshot
                return value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryDequeue([MaybeNullWhen(false)] out T result)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_list.Count > 0)
                {
                    result = _list.First!.Value;
                    _list.RemoveFirst();
                    _snapshotStale = true; // Invalidate the snapshot
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public T? Peek()
        {
            _lock.EnterReadLock();
            try
            {
                if (_list.Count == 0)
                {
                    return default;
                }

                return _list.First!.Value;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryPeek([MaybeNullWhen(false)] out T result)
        {
            _lock.EnterReadLock();
            try
            {
                if (_list.Count > 0)
                {
                    result = _list.First!.Value;
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool Remove(T value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_list.Remove(value))
                {
                    _snapshotStale = true;
                    return true;
                }

                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Remove(LinkedListNode<T> node)
        {
            _lock.EnterWriteLock();
            try
            {
                _list.Remove(node);
                _snapshotStale = true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryRemove(LinkedListNode<T> node)
        {
            if (node.List != _list)
            {
                return false;
            }

            Remove(node);
            return true;
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _list.Clear();
                _snapshotStale = true; // Invalidate the snapshot
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IReadOnlyList<LinkedListNode<T>> GetNodeSnapshot()
        {
            _lock.EnterReadLock();
            try
            {
                // Call .ToList to create a snapshot of the iteration result because the chain might break
                return _list.AsRemovable().ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public IEnumerable<T> Uncached()
        {
            _lock.EnterReadLock();
            try
            {
                return new List<T>(_list);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (!_cacheEnumerationSnapshot)
            {
                // Return an enumerator over a new snapshot
                return Uncached().GetEnumerator();
            }

            _lock.EnterUpgradeableReadLock();
            try
            {
                // Check if the snapshot is stale
                if (_snapshotStale)
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        // Recreate the snapshot if it is stale
                        _snapshot = new List<T>(_list);
                        _snapshotStale = false;
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                }

                // Return an enumerator over the cached snapshot
                return _snapshot!.GetEnumerator();
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
