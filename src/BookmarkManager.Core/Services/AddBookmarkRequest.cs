namespace BookmarkManager.Core.Services;

public record AddBookmarkRequest(string Url, string? Title, List<string>? Tags);
