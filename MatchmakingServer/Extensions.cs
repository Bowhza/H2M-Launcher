using Nito.Disposables;

namespace MatchmakingServer
{
    public static class Extensions
    {
        public static IEnumerable<LinkedListNode<T>> AsRemovable<T>(this LinkedList<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list");
            }
            var node = list.First;
            while (node != null)
            {
                var next = node.Next;
                yield return node;
                node = next;
            }
        }

        public static Task<bool> WaitOneAsync(this WaitHandle waitHandle, int timeoutMs = -1)
        {
            if (waitHandle == null)
                throw new ArgumentNullException(nameof(waitHandle));

            var tcs = new TaskCompletionSource<bool>();

            RegisteredWaitHandle registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                waitHandle,
                callBack: (state, timedOut) => { tcs.TrySetResult(!timedOut); },
                state: null,
                millisecondsTimeOutInterval: timeoutMs,
                executeOnlyOnce: true);

            return tcs.Task.ContinueWith((antecedent) =>
            {
                registeredWaitHandle.Unregister(waitObject: null);
                try
                {
                    return antecedent.Result;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task LockAll(this IEnumerable<SemaphoreSlim> items, Func<Task> asyncOperation)
        {
            // create local copy
            List<SemaphoreSlim> locks = items.ToList();

            // Step 1: Lock all items
            var lockTasks = new List<Task>();
            foreach (SemaphoreSlim item in locks)
            {
                lockTasks.Add(item.WaitAsync()); // Lock the item asynchronously
            }

            // Wait for all locks to be acquired
            await Task.WhenAll(lockTasks);

            try
            {
                // Step 2: Perform async operation
                await asyncOperation();
            }
            finally
            {
                // Step 3: Release all locks
                foreach (SemaphoreSlim item in locks)
                {
                    item.Release();
                }
            }
        }

        public static IDisposable LockAll(this IEnumerable<object> lockObjects)
        {
            _ = lockObjects.TryGetNonEnumeratedCount(out int count);
            List<object> enteredLocks = new(count);
            try
            {
                foreach (var lockObject in lockObjects)
                {
                    Monitor.Enter(lockObject);
                    enteredLocks.Add(lockObject);
                }

                return Disposable.Create(() =>
                {
                    foreach (var lockObject in enteredLocks)
                        Monitor.Exit(lockObject);
                });
            }
            catch
            {
                foreach (var lockObject in enteredLocks)
                    Monitor.Exit(lockObject);
                throw;
            }
        }
    }
}
