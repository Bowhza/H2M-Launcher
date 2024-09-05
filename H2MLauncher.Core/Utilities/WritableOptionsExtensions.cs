using Nogic.WritableOptions;

namespace H2MLauncher.Core.Utilities;

public static class WritableOptionsExtensions
{
    public static void Update<T>(this IWritableOptions<T> options, Func<T, T> updateFunc, bool reload = true)
        where T : class, new()
    {
        options.Update(updateFunc(options.CurrentValue), reload);
    }
}
