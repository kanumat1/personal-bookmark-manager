using BookmarkManager.Core.Common;
using BookmarkManager.Core.Models;
using BookmarkManager.Core.Repositories;
using BookmarkManager.Core.Validation;

namespace BookmarkManager.Core.Services;

/// <summary>
/// Runs the Add/Edit save pipeline: format/scheme validation → duplicate check → title fetch (only
/// when the user left the title blank) → persist. Each step can short-circuit with a failure result.
/// </summary>
public class BookmarkService
{
    private readonly IBookmarkRepository _repository;
    private readonly TitleFetchService _titleFetchService;

    public BookmarkService(IBookmarkRepository repository, TitleFetchService titleFetchService)
    {
        _repository = repository;
        _titleFetchService = titleFetchService;
    }

    public async Task<Result<Bookmark>> AddAsync(AddBookmarkRequest request, CancellationToken cancellationToken = default)
    {
        var validated = await ValidateAsync(request.Url, request.Title, request.Tags, excludeId: null, cancellationToken)
            .ConfigureAwait(false);
        if (!validated.IsSuccess)
        {
            return Result<Bookmark>.Failure(validated.Error!);
        }

        var (validUrl, normalizedUrl, title, tags) = validated.Value!;
        var now = DateTimeOffset.UtcNow;
        var bookmark = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = validUrl,
            NormalizedUrl = normalizedUrl,
            Title = title,
            Tags = tags,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(bookmark, cancellationToken).ConfigureAwait(false);
        return Result<Bookmark>.Success(bookmark);
    }

    /// <summary>Re-validates and re-checks duplicates (excluding itself) before overwriting URL/title/tags; CreatedAt is preserved.</summary>
    public async Task<Result<Bookmark>> UpdateAsync(Guid id, UpdateBookmarkRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return Result<Bookmark>.Failure("Bookmark not found.");
        }

        var validated = await ValidateAsync(request.Url, request.Title, request.Tags, excludeId: id, cancellationToken)
            .ConfigureAwait(false);
        if (!validated.IsSuccess)
        {
            return Result<Bookmark>.Failure(validated.Error!);
        }

        var (validUrl, normalizedUrl, title, tags) = validated.Value!;
        existing.Url = validUrl;
        existing.NormalizedUrl = normalizedUrl;
        existing.Title = title;
        existing.Tags = tags;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        return Result<Bookmark>.Success(existing);
    }

    /// <summary>Returns false if no bookmark with that Id exists; the caller decides how to surface that (e.g. 404).</summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<Bookmark?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    /// <summary>Shared skeleton behind Add and Edit: format → scheme allow-list → tag rules → duplicate check → title fetch.</summary>
    private async Task<Result<(string Url, string NormalizedUrl, string Title, List<string> Tags)>> ValidateAsync(
        string url, string? title, List<string>? tags, Guid? excludeId, CancellationToken cancellationToken)
    {
        var urlResult = UrlValidator.Validate(url);
        if (!urlResult.IsSuccess)
        {
            return Result<(string, string, string, List<string>)>.Failure(urlResult.Error!);
        }

        var validUrl = urlResult.Value!;
        var normalizedUrl = UrlValidator.Normalize(validUrl);

        var tagsResult = TagNormalizer.Normalize(tags);
        if (!tagsResult.IsSuccess)
        {
            return Result<(string, string, string, List<string>)>.Failure(tagsResult.Error!);
        }

        var existing = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Any(b => b.Id != excludeId && string.Equals(b.NormalizedUrl, normalizedUrl, StringComparison.Ordinal)))
        {
            return Result<(string, string, string, List<string>)>.Failure("This URL is already saved.");
        }

        string resolvedTitle;
        if (!string.IsNullOrWhiteSpace(title))
        {
            resolvedTitle = title.Trim();
        }
        else
        {
            var titleResult = await _titleFetchService.TryFetchTitleAsync(validUrl, cancellationToken).ConfigureAwait(false);
            if (!titleResult.IsSuccess)
            {
                return Result<(string, string, string, List<string>)>.Failure(
                    "We couldn't get a title automatically — please enter one.");
            }

            resolvedTitle = titleResult.Value!;
        }

        return Result<(string, string, string, List<string>)>.Success((validUrl, normalizedUrl, resolvedTitle, tagsResult.Value!));
    }

    /// <summary>
    /// Returns bookmarks newest-first, optionally filtered to a single tag, to bookmarks with zero
    /// tags ("Untagged"), and/or a case-insensitive title/URL search term; an empty result is a
    /// valid "no matches" state, not an error.
    /// </summary>
    public async Task<IReadOnlyList<Bookmark>> GetAllAsync(string? tag = null, bool untagged = false,
        string? search = null, CancellationToken cancellationToken = default)
    {
        var bookmarks = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Bookmark> filtered = bookmarks;

        if (untagged)
        {
            filtered = filtered.Where(b => b.Tags.Count == 0);
        }
        else if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.Trim();
            filtered = filtered.Where(b => b.Tags.Contains(normalizedTag, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(b =>
                b.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.Url.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.OrderByDescending(b => b.CreatedAt).ToList();
    }

    /// <summary>Distinct tags across all bookmarks, sorted alphabetically, for the filter dropdown.</summary>
    public async Task<IReadOnlyList<string>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        var bookmarks = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return bookmarks.SelectMany(b => b.Tags)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
    }
}
