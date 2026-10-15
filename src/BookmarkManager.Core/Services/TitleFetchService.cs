using BookmarkManager.Core.Common;

namespace BookmarkManager.Core.Services;

/// <summary>Thin orchestration boundary between the save pipeline and the title-fetch adapter, kept fakeable in tests.</summary>
public class TitleFetchService
{
    private readonly ITitlePageFetcher _fetcher;

    public TitleFetchService(ITitlePageFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    public Task<Result<string>> TryFetchTitleAsync(string url, CancellationToken cancellationToken = default) =>
        _fetcher.FetchTitleAsync(new Uri(url), cancellationToken);
}
