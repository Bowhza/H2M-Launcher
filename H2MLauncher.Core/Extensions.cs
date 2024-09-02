using System.Security;

using Nogic.WritableOptions;

namespace H2MLauncher.Core;

public static class Extensions
{
    public static bool IsNullOrEmpty(this SecureString secureString)
    {
        return secureString == null || secureString.Length == 0;
    }

    public static int IndexOfFirst<T>(this IList<T> list, Func<T, bool> predicate)
    {
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (predicate(list[i]))
            {
                return i;
            }
        }

        return -1;
    }

    public static bool AddOrUpdate<T>(this IList<T> list, T newValue, Func<T, bool> predicate,
        IComparer<T>? comparer = null)
    {
        int index = IndexOfFirst(list, predicate);

        if (index == -1)
        {
            if (comparer == null)
            {
                list.Add(newValue);
            }
            else
            {
                InsertUsingComparer(list, comparer, newValue);
            }

            return true;
        }

        list[index] = newValue;

        return false;
    }

    private static void InsertUsingComparer<T>(IList<T> list, IComparer<T> comparer, T value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (comparer.Compare(value, list[i]) <= 0)
            {
                list.Insert(i, value);

                return;
            }
        }

        list.Add(value);
    }

    public static bool AddOrUpdate<T, TSelect>(this IList<T> list, T newValue, Func<T, TSelect> selector,
        IComparer<T>? comparer = null)
    {
        if (selector == null) { return false; }

        return AddOrUpdate(list, newValue,
            (item) => EqualityComparer<TSelect>.Default.Equals(selector.Invoke(item), selector.Invoke(newValue)),
            comparer);
    }

    public static bool AddOrUpdate<T>(this IList<T> list, T newValue, IEqualityComparer<T>? equalityComparer,
        IComparer<T>? comparer = null)
    {
        return AddOrUpdate(list, newValue,
            (item) => equalityComparer?.Equals(item, newValue) ?? EqualityComparer<T>.Default.Equals(item, newValue),
            comparer);
    }

    public static void Update<T>(this IWritableOptions<T> options, Func<T, T> updateFunc, bool reload = true)
        where T : class, new()
    {
        options.Update(updateFunc(options.CurrentValue), reload);
    }
}
