using BookmarkManager.Core.Common;

namespace BookmarkManager.Core.Validation;

/// <summary>Normalizes free-text tags: trims, lowercases, dedupes, and enforces a max length.</summary>
public static class TagNormalizer
{
    private const int MaxTagLength = 50;

    public static Result<List<string>> Normalize(IEnumerable<string>? rawTags)
    {
        if (rawTags is null)
        {
            return Result<List<string>>.Success(new List<string>());
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>();

        foreach (var rawTag in rawTags)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                continue;
            }

            var tag = rawTag.Trim().ToLowerInvariant();

            if (tag.Length > MaxTagLength)
            {
                return Result<List<string>>.Failure($"Tags must be {MaxTagLength} characters or fewer.");
            }

            if (seen.Add(tag))
            {
                normalized.Add(tag);
            }
        }

        return Result<List<string>>.Success(normalized);
    }
}
