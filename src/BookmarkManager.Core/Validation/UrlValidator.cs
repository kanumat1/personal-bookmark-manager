using BookmarkManager.Core.Common;

namespace BookmarkManager.Core.Validation;

/// <summary>Validates and normalizes user-supplied URLs before they reach the service/repository layer.</summary>
public static class UrlValidator
{
    private const int MaxUrlLength = 2048;
    private static readonly string[] AllowedSchemes = { Uri.UriSchemeHttp, Uri.UriSchemeHttps };

    /// <summary>Returns the trimmed, valid absolute URL on success, or a user-friendly error on failure.</summary>
    public static Result<string> Validate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result<string>.Failure("Enter a valid http(s) URL.");
        }

        var trimmed = url.Trim();

        if (trimmed.Length > MaxUrlLength)
        {
            return Result<string>.Failure($"URL must be {MaxUrlLength} characters or fewer.");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            !AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return Result<string>.Failure("Enter a valid http(s) URL.");
        }

        return Result<string>.Success(trimmed);
    }

    /// <summary>Lowercases scheme/host and trims a trailing slash so visually-different-but-same URLs compare equal.</summary>
    public static string Normalize(string validAbsoluteUrl)
    {
        var uri = new Uri(validAbsoluteUrl);
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant()
        };

        var path = builder.Path;
        if (path.Length > 1 && path.EndsWith('/'))
        {
            builder.Path = path.TrimEnd('/');
        }

        // Default ports are implicit in the Uri comparison; Uri.ToString omits them when they match the scheme default.
        return builder.Uri.ToString();
    }
}
