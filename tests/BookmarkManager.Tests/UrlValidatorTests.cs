using BookmarkManager.Core.Validation;

namespace BookmarkManager.Tests;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("htp:/bad")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://example.com/file")]
    public void Validate_RejectsInvalidOrDisallowedSchemeUrls(string? url)
    {
        var result = UrlValidator.Validate(url);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?query=1")]
    public void Validate_AcceptsWellFormedHttpAndHttpsUrls(string url)
    {
        var result = UrlValidator.Validate(url);

        Assert.True(result.IsSuccess);
        Assert.Equal(url, result.Value);
    }

    [Fact]
    public void Validate_RejectsUrlLongerThanMaxLength()
    {
        var longUrl = "https://example.com/" + new string('a', 2100);

        var result = UrlValidator.Validate(longUrl);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("https://Example.com/", "https://example.com/")]
    [InlineData("HTTPS://EXAMPLE.COM/path/", "https://example.com/path")]
    [InlineData("https://example.com", "https://example.com/")]
    public void Normalize_LowercasesSchemeAndHostAndTrimsTrailingSlash(string input, string expected)
    {
        var normalized = UrlValidator.Normalize(input);

        Assert.Equal(expected, normalized);
    }
}
