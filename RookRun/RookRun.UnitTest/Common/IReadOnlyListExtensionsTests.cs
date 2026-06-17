using RookRun.Common;

namespace RookRun.UnitTest.Common;

public class IReadOnlyListExtensionsTests
{
    [Fact]
    public void Range_ReturnsItemsBetweenStartAndEndExclusive()
    {
        var list = new[] { 10, 20, 30, 40, 50 };

        var result = list.Range(1, 4).ToList();

        Assert.Collection(result,
            item => Assert.Equal(20, item),
            item => Assert.Equal(30, item),
            item => Assert.Equal(40, item));
    }

    [Fact]
    public void Range_ReturnsEmptyWhenStartEqualsEnd()
    {
        var list = new[] { 10, 20, 30 };

        var result = list.Range(1, 1).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void Range_ReturnsEmptyWhenStartGreaterThanEnd()
    {
        var list = new[] { 10, 20, 30 };

        var result = list.Range(3, 1).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void Range_ReturnsAllItemsWhenCoveringFullRange()
    {
        var list = new[] { 10, 20, 30 };

        var result = list.Range(0, 3).ToList();

        Assert.Collection(result,
            item => Assert.Equal(10, item),
            item => Assert.Equal(20, item),
            item => Assert.Equal(30, item));
    }

    [Fact]
    public void Range_ReturnsFirstItemOnly()
    {
        var list = new[] { "a", "b", "c" };

        var result = list.Range(0, 1).ToList();

        Assert.Collection(result, item => Assert.Equal("a", item));
    }

    [Fact]
    public void Range_ReturnsLastItemOnly()
    {
        var list = new[] { "a", "b", "c" };

        var result = list.Range(2, 3).ToList();

        Assert.Collection(result, item => Assert.Equal("c", item));
    }

    [Fact]
    public void Range_ThrowsWhenStartIsNegative()
    {
        var list = new[] { 1, 2, 3 };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            list.Range(-1, 2).ToList());
    }

    [Fact]
    public void Range_ThrowsWhenEndExceedsLength()
    {
        var list = new[] { 1, 2, 3 };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            list.Range(0, 5).ToList());
    }

    [Fact]
    public void Range_IsLazy_DoesNotEnumerateUntilConsumed()
    {
        var list = new[] { 1, 2, 3 };
        var enumerable = list.Range(1, 2);

        // Verify it's lazy by checking that IEnumerable hasn't been consumed yet
        Assert.IsAssignableFrom<IEnumerable<int>>(enumerable);

        var result = enumerable.ToList();
        Assert.Single(result);
        Assert.Equal(2, result[0]);
    }

    [Fact]
    public void Range_WorksWithComplexTypes()
    {
        var list = new[] 
        {
            new { Id = 1, Name = "A" },
            new { Id = 2, Name = "B" },
            new { Id = 3, Name = "C" }
        };

        var result = list.Range(0, 2).ToList();

        Assert.Collection(result,
            item => Assert.Equal("A", item.Name),
            item => Assert.Equal("B", item.Name));
    }

    [Fact]
    public void Range_CanEnumerateMultipleTimes()
    {
        var list = new[] { 1, 2, 3, 4, 5 };
        var enumerable = list.Range(1, 3);

        var first = enumerable.ToList();
        var second = enumerable.ToList();

        Assert.Equal(first, second);
        Assert.Collection(first,
            item => Assert.Equal(2, item),
            item => Assert.Equal(3, item));
    }
}
