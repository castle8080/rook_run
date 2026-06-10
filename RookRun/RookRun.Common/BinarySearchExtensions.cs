using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.Common;

/// <summary>
/// Provides binary search helpers for locating boundaries within sorted read-only lists.
/// </summary>
public static class BinarySearchExtensions
{
    /// <summary>
    /// Finds the first index whose selected key is greater than or equal to the supplied value.
    /// </summary>
    /// <typeparam name="T">The item type stored in the list.</typeparam>
    /// <typeparam name="TKey">The key type produced by <paramref name="keySelector" />.</typeparam>
    /// <param name="items">The sorted list to search.</param>
    /// <param name="value">The value to compare against.</param>
    /// <param name="keySelector">Selects the comparable key for each item.</param>
    /// <returns>The first index whose selected key is greater than or equal to <paramref name="value" />.</returns>
    public static int LowerBound<T, TKey>(
        this IReadOnlyList<T> items,
        TKey value,
        Func<T, TKey> keySelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keySelector);

        return BoundarySearch(
            items,
            value,
            keySelector,
            inclusive: false);
    }

    /// <summary>
    /// Finds the first index whose selected key is greater than the supplied value.
    /// </summary>
    /// <typeparam name="T">The item type stored in the list.</typeparam>
    /// <typeparam name="TKey">The key type produced by <paramref name="keySelector" />.</typeparam>
    /// <param name="items">The sorted list to search.</param>
    /// <param name="value">The value to compare against.</param>
    /// <param name="keySelector">Selects the comparable key for each item.</param>
    /// <returns>The first index whose selected key is greater than <paramref name="value" />.</returns>
    public static int UpperBound<T, TKey>(
        this IReadOnlyList<T> items,
        TKey value,
        Func<T, TKey> keySelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keySelector);

        return BoundarySearch(
            items,
            value,
            keySelector,
            inclusive: true);
    }

    /// <summary>
    /// Finds the inclusive range of items whose selected keys fall between the supplied minimum and maximum values.
    /// </summary>
    /// <typeparam name="T">The item type stored in the list.</typeparam>
    /// <typeparam name="TKey">The key type produced by <paramref name="keySelector" />.</typeparam>
    /// <param name="items">The sorted list to search.</param>
    /// <param name="minInclusive">The minimum key value to include.</param>
    /// <param name="maxInclusive">The maximum key value to include.</param>
    /// <param name="keySelector">Selects the comparable key for each item.</param>
    /// <returns>A tuple containing the start index and the exclusive end index of the matching range.</returns>
    public static (int Start, int EndExclusive) BinarySearchRange<T, TKey>(
        this IReadOnlyList<T> items,
        TKey minInclusive,
        TKey maxInclusive,
        Func<T, TKey> keySelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keySelector);

        int start = items.LowerBound(minInclusive, keySelector);
        int end = items.UpperBound(maxInclusive, keySelector);

        return (start, end);
    }

    /// <summary>
    /// Performs the shared boundary search used by lower-bound and upper-bound lookups.
    /// </summary>
    /// <typeparam name="T">The item type stored in the list.</typeparam>
    /// <typeparam name="TKey">The key type produced by <paramref name="keySelector" />.</typeparam>
    /// <param name="items">The sorted list to search.</param>
    /// <param name="value">The comparison value.</param>
    /// <param name="keySelector">Selects the comparable key for each item.</param>
    /// <param name="inclusive">
    /// <see langword="true" /> to search for the first key greater than <paramref name="value" />; otherwise, search for the first key greater than or equal to <paramref name="value" />.
    /// </param>
    /// <returns>The boundary index that matches the requested search mode.</returns>
    private static int BoundarySearch<T, TKey>(
        IReadOnlyList<T> items,
        TKey value,
        Func<T, TKey> keySelector,
        bool inclusive)
    {
        var comparer = Comparer<TKey>.Default;

        int left = 0;
        int right = items.Count;

        while (left < right)
        {
            int mid = left + ((right - left) / 2);

            int comparison =
                comparer.Compare(keySelector(items[mid]), value);

            if (inclusive
                ? comparison <= 0 // UpperBound: first key > value
                : comparison < 0) // LowerBound: first key >= value
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        return left;
    }
}