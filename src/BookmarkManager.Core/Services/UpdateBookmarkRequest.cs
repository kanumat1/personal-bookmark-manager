namespace BookmarkManager.Core.Services;

public record UpdateBookmarkRequest(string Url, string? Title, List<string>? Tags);
