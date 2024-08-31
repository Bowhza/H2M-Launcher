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
    }
}
