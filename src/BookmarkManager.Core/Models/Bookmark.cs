namespace BookmarkManager.Core.Models;

public class Bookmark
{
    public Guid Id { get; set; }

    /// <summary>Original, user-entered URL, kept as-typed for display.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Lowercased scheme/host, trimmed trailing slash — used only for duplicate comparison.</summary>
    public string NormalizedUrl { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
