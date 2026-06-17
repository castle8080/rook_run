using RookRun.ObjectStore;

namespace RookRun.UnitTest.ObjectStore;

public class ObjectStorePathTests
{
    [Theory]
    [InlineData("simple.json")]
    [InlineData("folder/file.json")]
    [InlineData("deep/nested/folder/file.json")]
    [InlineData("with-dashes/file-name.json")]
    [InlineData("with_underscores/file_name.json")]
    [InlineData("123/456/file.json")]
    public void NormalizeRequiredPath_ReturnsNormalizedPath(string path)
    {
        var result = ObjectStorePath.NormalizeRequiredPath(path);
        Assert.Equal(path, result);
    }

    [Fact]
    public void NormalizeRequiredPath_ConvertsBackslashesToForwardSlashes()
    {
        var result = ObjectStorePath.NormalizeRequiredPath("folder\\subfolder\\file.json");
        Assert.Equal("folder/subfolder/file.json", result);
    }

    [Fact]
    public void NormalizeRequiredPath_RemovesLeadingSlashes()
    {
        var result = ObjectStorePath.NormalizeRequiredPath("/folder/file.json");
        Assert.Equal("folder/file.json", result);
    }

    [Fact]
    public void NormalizeRequiredPath_RemovesTrailingSlashes()
    {
        var result = ObjectStorePath.NormalizeRequiredPath("folder/file.json/");
        Assert.Equal("folder/file.json", result);
    }

    [Fact]
    public void NormalizeRequiredPath_RemovesBothLeadingAndTrailingSlashes()
    {
        var result = ObjectStorePath.NormalizeRequiredPath("/folder/file.json/");
        Assert.Equal("folder/file.json", result);
    }

    [Fact]
    public void NormalizeRequiredPath_NormalizesComplexPath()
    {
        var result = ObjectStorePath.NormalizeRequiredPath("\\folder\\subfolder\\file.json\\");
        Assert.Equal("folder/subfolder/file.json", result);
    }

    [Fact]
    public void NormalizeRequiredPath_ThrowsWhenPathIsNull()
    {
            Assert.Throws<ArgumentNullException>(() =>
                ObjectStorePath.NormalizeRequiredPath(null!));
    }

    [Fact]
    public void NormalizeRequiredPath_ThrowsWhenPathIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizeRequiredPath(string.Empty));
    }

    [Fact]
    public void NormalizeRequiredPath_ThrowsWhenPathIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizeRequiredPath("   "));
    }

    [Fact]
    public void NormalizeRequiredPath_ThrowsWhenPathBecomesEmptyAfterNormalization()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizeRequiredPath("/////"));
    }

    [Fact]
    public void NormalizeRequiredPath_ThrowsWhenPathContainsDotSegment()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizeRequiredPath("folder/./file.json"));
    }

    [Fact]
    public void NormalizeRequiredPath_ThrowsWhenPathContainsDotDotSegment()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizeRequiredPath("folder/../file.json"));
    }

    [Fact]
    public void NormalizeRequiredPath_ThrowsWhenPathContainsDotSegmentAtStart()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizeRequiredPath("./folder/file.json"));
    }

    [Fact]
    public void NormalizeRequiredPath_ThrowsWhenPathContainsDotDotSegmentAtStart()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizeRequiredPath("../folder/file.json"));
    }

    [Theory]
    [InlineData("folder/file.json")]
    [InlineData("deep/nested/folder/file.json")]
    public void NormalizePrefix_ReturnsNormalizedPrefix(string prefix)
    {
        var result = ObjectStorePath.NormalizePrefix(prefix);
        Assert.Equal(prefix, result);
    }

    [Fact]
    public void NormalizePrefix_ConvertsBackslashesToForwardSlashes()
    {
        var result = ObjectStorePath.NormalizePrefix("folder\\subfolder");
        Assert.Equal("folder/subfolder", result);
    }

    [Fact]
    public void NormalizePrefix_RemovesLeadingSlashes()
    {
        var result = ObjectStorePath.NormalizePrefix("/folder");
        Assert.Equal("folder", result);
    }

    [Fact]
    public void NormalizePrefix_RemovesTrailingSlashes()
    {
        var result = ObjectStorePath.NormalizePrefix("folder/");
        Assert.Equal("folder", result);
    }

    [Fact]
    public void NormalizePrefix_RemovesBothLeadingAndTrailingSlashes()
    {
        var result = ObjectStorePath.NormalizePrefix("/folder/");
        Assert.Equal("folder", result);
    }

    [Fact]
    public void NormalizePrefix_NormalizesComplexPrefix()
    {
        var result = ObjectStorePath.NormalizePrefix("\\folder\\subfolder\\");
        Assert.Equal("folder/subfolder", result);
    }

    [Fact]
    public void NormalizePrefix_ReturnsEmptyWhenPrefixIsNull()
    {
        var result = ObjectStorePath.NormalizePrefix(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizePrefix_ReturnsEmptyWhenPrefixIsEmpty()
    {
        var result = ObjectStorePath.NormalizePrefix(string.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizePrefix_ReturnsEmptyWhenPrefixIsWhitespace()
    {
        var result = ObjectStorePath.NormalizePrefix("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizePrefix_ReturnsEmptyWhenPrefixBecomesEmptyAfterNormalization()
    {
        var result = ObjectStorePath.NormalizePrefix("/////");
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizePrefix_ThrowsWhenPrefixContainsDotSegment()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizePrefix("folder/./subfolder"));
    }

    [Fact]
    public void NormalizePrefix_ThrowsWhenPrefixContainsDotDotSegment()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizePrefix("folder/../subfolder"));
    }

    [Fact]
    public void NormalizePrefix_ThrowsWhenPrefixContainsDotSegmentAtStart()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizePrefix("./folder"));
    }

    [Fact]
    public void NormalizePrefix_ThrowsWhenPrefixContainsDotDotSegmentAtStart()
    {
        Assert.Throws<ArgumentException>(() =>
            ObjectStorePath.NormalizePrefix("../folder"));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("valid/path")]
    [InlineData("valid/deep/path")]
    public void NormalizePrefix_AllowsValidSegmentNames(string prefix)
    {
        var result = ObjectStorePath.NormalizePrefix(prefix);
        Assert.NotEmpty(result);
    }
}
