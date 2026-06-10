using RookRun.Common;

namespace RookRun.UnitTest.Common;

/// <summary>
/// Verifies the boundary calculations produced by <see cref="BinarySearchExtensions" />.
/// </summary>
public class BinarySearchExtensionsTests
{
    /// <summary>
    /// Ensures <see cref="BinarySearchExtensions.LowerBound{T, TKey}(IReadOnlyList{T}, TKey, Func{T, TKey})" /> returns the first matching duplicate index.
    /// </summary>
    [Fact]
    public void LowerBound_ReturnsFirstMatchingIndex()
    {
        IReadOnlyList<int> items = [1, 3, 3, 3, 5, 7];

        int index = items.LowerBound(3, value => value);

        Assert.Equal(1, index);
    }

    /// <summary>
    /// Ensures <see cref="BinarySearchExtensions.UpperBound{T, TKey}(IReadOnlyList{T}, TKey, Func{T, TKey})" /> returns the first index after the last matching duplicate.
    /// </summary>
    [Fact]
    public void UpperBound_ReturnsIndexAfterLastMatchingValue()
    {
        IReadOnlyList<int> items = [1, 3, 3, 3, 5, 7];

        int index = items.UpperBound(3, value => value);

        Assert.Equal(4, index);
    }

    /// <summary>
    /// Ensures <see cref="BinarySearchExtensions.BinarySearchRange{T, TKey}(IReadOnlyList{T}, TKey, TKey, Func{T, TKey})" /> returns the inclusive matching span.
    /// </summary>
    [Fact]
    public void BinarySearchRange_ReturnsMatchingWindow()
    {
        IReadOnlyList<int> items = [1, 3, 3, 3, 5, 7, 9];

        var range = items.BinarySearchRange(3, 7, value => value);

        Assert.Equal(1, range.Start);
        Assert.Equal(6, range.EndExclusive);
    }

    /// <summary>
    /// Ensures range searches return an empty span when no selected keys fall within the requested bounds.
    /// </summary>
    [Fact]
    public void BinarySearchRange_ReturnsEmptyRangeWhenNothingMatches()
    {
        IReadOnlyList<int> items = [1, 3, 5, 7];

        var range = items.BinarySearchRange(8, 10, value => value);

        Assert.Equal(4, range.Start);
        Assert.Equal(4, range.EndExclusive);
    }

    /// <summary>
    /// Ensures boundary searches work with projected keys instead of direct values.
    /// </summary>
    [Fact]
    public void BoundarySearches_UseProjectedKeys()
    {
        IReadOnlyList<SearchItem> items =
        [
            new(1, "one"),
            new(3, "three-a"),
            new(3, "three-b"),
            new(5, "five")
        ];

        int lowerBound = items.LowerBound(3, item => item.Key);
        int upperBound = items.UpperBound(3, item => item.Key);

        Assert.Equal(1, lowerBound);
        Assert.Equal(3, upperBound);
    }

    /// <summary>
    /// Ensures null item lists are rejected before searching.
    /// </summary>
    [Fact]
    public void LowerBound_ThrowsWhenItemsIsNull()
    {
        IReadOnlyList<int>? items = null;

        Assert.Throws<ArgumentNullException>(() => items!.LowerBound(3, value => value));
    }

    /// <summary>
    /// Ensures null key selectors are rejected before searching.
    /// </summary>
    [Fact]
    public void UpperBound_ThrowsWhenKeySelectorIsNull()
    {
        IReadOnlyList<int> items = [1, 3, 5];

        Assert.Throws<ArgumentNullException>(() => items.UpperBound(3, null!));
    }

    private sealed record SearchItem(int Key, string Value);
}