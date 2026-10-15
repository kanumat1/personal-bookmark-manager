using BookmarkManager.Core.Validation;

namespace BookmarkManager.Tests;

public class TagNormalizerTests
{
    [Fact]
    public void Normalize_WithNullTags_ReturnsEmptyList()
    {
        var result = TagNormalizer.Normalize(null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void Normalize_TrimsAndLowercasesTags()
    {
        var result = TagNormalizer.Normalize(new[] { "  Work  ", "READING" });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "work", "reading" }, result.Value);
    }

    [Fact]
    public void Normalize_DedupesCaseInsensitiveDuplicates()
    {
        var result = TagNormalizer.Normalize(new[] { "Work", "work", " WORK " });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "work" }, result.Value);
    }

    [Fact]
    public void Normalize_SkipsBlankEntries()
    {
        var result = TagNormalizer.Normalize(new[] { "", "   ", "reading" });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "reading" }, result.Value);
    }

    [Fact]
    public void Normalize_WithEmptyList_ReturnsEmptyList()
    {
        var result = TagNormalizer.Normalize(new List<string>());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void Normalize_WithTagLongerThanMaxLength_ReturnsFailure()
    {
        var result = TagNormalizer.Normalize(new[] { new string('a', 51) });

        Assert.False(result.IsSuccess);
    }
}
