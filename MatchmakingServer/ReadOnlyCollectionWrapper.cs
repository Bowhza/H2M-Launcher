using System.Collections;

namespace MatchmakingServer;

public class ReadOnlyCollectionWrapper<T>(ICollection<T> collection) : IReadOnlyCollection<T>
{
    public int Count => collection.Count;

    public IEnumerator<T> GetEnumerator() => collection.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => collection.GetEnumerator();
}
