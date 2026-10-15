using BookmarkManager.Core.Common;

namespace BookmarkManager.Core.Services;

/// <summary>Adapter boundary for fetching a page's title, so the HTTP/SSRF/timeout mechanics are swappable and fakeable in tests.</summary>
public interface ITitlePageFetcher
{
    Task<Result<string>> FetchTitleAsync(Uri url, CancellationToken cancellationToken = default);
}
