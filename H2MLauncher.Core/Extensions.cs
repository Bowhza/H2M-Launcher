using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace H2MLauncher.Core;

internal static class Extensions
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

    /// <summary>
    /// Dynamically update a list with new values by inserting them at the right place using a <paramref name="insertComparer"/>,
    /// or replacing a existing value matched with the given <paramref name="equalityPredicate"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="newValues">The values to merge with the list.</param>
    /// <param name="equalityPredicate">A predicate that evaluates whether two values are equal.</param>
    /// <param name="insertComparer">A comparer for inserting the new values at the correct position.</param>
    /// <param name="removeOldValues">Remove old values that are not replaced by a new value.</param>
    public static void DynamicUpdate<T>(this IList<T> list,
        IEnumerable<T> newValues,
        Func<T, T, bool>? equalityPredicate = null,
        IComparer<T>? insertComparer = null,
        bool removeOldValues = true)
    {
        if (removeOldValues)
        {
            var valuesToRemove = list
                        .Where(oldValue => !newValues.Any(newValue =>
                            equalityPredicate?.Invoke(oldValue, newValue) ??
                                EqualityComparer<T>.Default.Equals(oldValue, newValue)))
                        .ToList();

            foreach (var value in valuesToRemove)
            {
                list.Remove(value);
            }
        }
        foreach (var value in newValues)
        {
            list.AddOrUpdate(
                value,
                oldValue => equalityPredicate?.Invoke(value, oldValue) ??
                    EqualityComparer<T>.Default.Equals(oldValue, value),
                insertComparer);
        }
    }
}
