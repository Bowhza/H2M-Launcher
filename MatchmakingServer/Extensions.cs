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
    }
}
