using System.Runtime.CompilerServices;

namespace H2MLauncher.Core.Utilities;

public static class AsyncEnumerableExtensions
{
    // see: https://stackoverflow.com/revisions/70711683/4
    public static IAsyncEnumerable<T> Interleave<T>(params IAsyncEnumerable<T>[] sources) => Interleave(default, sources);

    public static IAsyncEnumerable<T> Interleave<T>(this IEnumerable<IAsyncEnumerable<T>> sources) => Interleave(default, sources.ToArray());

    public static async IAsyncEnumerable<T> Interleave<T>([EnumeratorCancellation] CancellationToken token, params IAsyncEnumerable<T>[] sources)
    {
        if (sources.Length == 0)
            yield break;
        var enumerators = new List<(IAsyncEnumerator<T> e, Task<bool> moveNextTask)>(sources.Length);
        try
        {
            for (int i = 0; i < sources.Length; i++)
            {
                IAsyncEnumerator<T> e = sources[i].GetAsyncEnumerator(token);
                enumerators.Add((e, e.MoveNextAsync().AsTask()));
            }

            do
            {
                Task<bool> taskResult = await Task.WhenAny(enumerators.Select(tuple => tuple.moveNextTask));
                int nextIndex = enumerators.FindIndex(tuple => tuple.moveNextTask == taskResult);
                var (enumerator, task) = enumerators[nextIndex];
                enumerators.RemoveAt(nextIndex);
                if (taskResult.Result)
                {
                    yield return enumerator.Current;
                    enumerators.Add((enumerator, enumerator.MoveNextAsync().AsTask()));
                }
                else
                {
                    try
                    {
                        await enumerator.DisposeAsync();
                    }
                    catch { }
                }
            } while (enumerators.Count > 0);
        }
        finally
        {
            for (int i = 0; i < enumerators.Count; i++)
            {
                try
                {
                    await enumerators[i].e.DisposeAsync();
                }
                catch { }
            }
        }
    }
}
