using BookmarkManager.Core.Common;
using BookmarkManager.Core.Models;
using BookmarkManager.Core.Repositories;
using BookmarkManager.Core.Services;

namespace BookmarkManager.Tests;

public class BookmarkServiceTests
{
    private class InMemoryBookmarkRepository : IBookmarkRepository
    {
        private readonly List<Bookmark> _bookmarks = new();

        public Task<IReadOnlyList<Bookmark>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Bookmark>>(_bookmarks.ToList());

        public Task<Bookmark?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_bookmarks.FirstOrDefault(b => b.Id == id));

        public Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
        {
            _bookmarks.Add(bookmark);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
        {
            var index = _bookmarks.FindIndex(b => b.Id == bookmark.Id);
            _bookmarks[index] = bookmark;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _bookmarks.RemoveAll(b => b.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeTitlePageFetcher : ITitlePageFetcher
    {
        private readonly Result<string> _result;

        public FakeTitlePageFetcher(Result<string> result) => _result = result;

        public Task<Result<string>> FetchTitleAsync(Uri url, CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
    }

    private static BookmarkService CreateService(IBookmarkRepository repository, Result<string>? titleFetchResult = null) =>
        new(repository, new TitleFetchService(new FakeTitlePageFetcher(titleFetchResult ?? Result<string>.Success("Fetched Title"))));

    [Fact]
    public async Task AddAsync_WithUserTypedTitle_SavesWithoutFetching()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository, Result<string>.Failure("should not be called"));

        var result = await service.AddAsync(new AddBookmarkRequest("https://example.com", "My Title", null));

        Assert.True(result.IsSuccess);
        Assert.Equal("My Title", result.Value!.Title);
    }

    [Fact]
    public async Task AddAsync_WithBlankTitle_FetchesTitleFromPage()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository, Result<string>.Success("Fetched Title"));

        var result = await service.AddAsync(new AddBookmarkRequest("https://example.com", null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("Fetched Title", result.Value!.Title);
    }

    [Fact]
    public async Task AddAsync_WhenTitleFetchFails_BlocksSaveAndDoesNotPersist()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository, Result<string>.Failure("timeout"));

        var result = await service.AddAsync(new AddBookmarkRequest("https://example.com", null, null));

        Assert.False(result.IsSuccess);
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_WithInvalidUrl_ReturnsFailureAndDoesNotPersist()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);

        var result = await service.AddAsync(new AddBookmarkRequest("javascript:alert(1)", "Title", null));

        Assert.False(result.IsSuccess);
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_WithDuplicateNormalizedUrl_ReturnsFailure()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://example.com/", "First", null));

        var result = await service.AddAsync(new AddBookmarkRequest("https://Example.com", "Second", null));

        Assert.False(result.IsSuccess);
        Assert.Equal("This URL is already saved.", result.Error);
        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_WithNoTags_SavesWithEmptyTagList()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);

        var result = await service.AddAsync(new AddBookmarkRequest("https://example.com", "Title", null));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Tags);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateCaseInsensitiveTags_SavesDedupedLowercasedTags()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);

        var result = await service.AddAsync(new AddBookmarkRequest("https://example.com", "Title",
            new List<string> { " Work ", "WORK", "Reading" }));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "work", "reading" }, result.Value!.Tags);
    }

    [Fact]
    public async Task AddAsync_WithTagLongerThanMaxLength_ReturnsFailureAndDoesNotPersist()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);

        var result = await service.AddAsync(new AddBookmarkRequest("https://example.com", "Title",
            new List<string> { new string('a', 51) }));

        Assert.False(result.IsSuccess);
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_WhenNoBookmarksSaved_ReturnsEmptyList()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);

        var result = await service.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsBookmarksNewestFirst()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://first.example.com", "First", null));
        await service.AddAsync(new AddBookmarkRequest("https://second.example.com", "Second", null));
        await service.AddAsync(new AddBookmarkRequest("https://third.example.com", "Third", null));

        var result = await service.GetAllAsync();

        Assert.Equal(new[] { "Third", "Second", "First" }, result.Select(b => b.Title));
    }

    [Fact]
    public async Task GetAllAsync_FilteredByTag_ReturnsOnlyMatchingBookmarksNewestFirst()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", new List<string> { "work" }));
        await service.AddAsync(new AddBookmarkRequest("https://b.example.com", "B", new List<string> { "reading" }));
        await service.AddAsync(new AddBookmarkRequest("https://c.example.com", "C", new List<string> { "WORK" }));

        var result = await service.GetAllAsync(tag: "Work");

        Assert.Equal(new[] { "C", "A" }, result.Select(b => b.Title));
    }

    [Fact]
    public async Task GetAllAsync_FilteredByTagWithNoMatches_ReturnsEmptyList()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", new List<string> { "work" }));

        var result = await service.GetAllAsync(tag: "missing");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_FilteredByUntagged_ReturnsOnlyBookmarksWithZeroTags()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", new List<string> { "work" }));
        await service.AddAsync(new AddBookmarkRequest("https://b.example.com", "B", null));

        var result = await service.GetAllAsync(untagged: true);

        Assert.Equal(new[] { "B" }, result.Select(b => b.Title));
    }

    [Fact]
    public async Task GetAllTagsAsync_ReturnsDistinctSortedTagsAcrossAllBookmarks()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", new List<string> { "work", "reading" }));
        await service.AddAsync(new AddBookmarkRequest("https://b.example.com", "B", new List<string> { "WORK" }));
        await service.AddAsync(new AddBookmarkRequest("https://c.example.com", "C", null));

        var result = await service.GetAllTagsAsync();

        Assert.Equal(new[] { "reading", "work" }, result);
    }

    [Fact]
    public async Task GetAllAsync_SearchMatchesTitleCaseInsensitive()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "Learning ASP.NET", null));
        await service.AddAsync(new AddBookmarkRequest("https://b.example.com", "Cooking recipes", null));

        var result = await service.GetAllAsync(search: "learning");

        Assert.Equal(new[] { "Learning ASP.NET" }, result.Select(b => b.Title));
    }

    [Fact]
    public async Task GetAllAsync_SearchMatchesUrl()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://docs.example.com/guide", "Guide", null));
        await service.AddAsync(new AddBookmarkRequest("https://other.example.com", "Other", null));

        var result = await service.GetAllAsync(search: "docs.example");

        Assert.Equal(new[] { "Guide" }, result.Select(b => b.Title));
    }

    [Fact]
    public async Task GetAllAsync_SearchWithNoMatches_ReturnsEmptyList()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", null));

        var result = await service.GetAllAsync(search: "nomatch");

        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateAsync_ChangesUrlTitleAndTags_PreservesCreatedAt()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        var added = await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", new List<string> { "work" }));
        var originalCreatedAt = added.Value!.CreatedAt;

        var result = await service.UpdateAsync(added.Value!.Id,
            new UpdateBookmarkRequest("https://b.example.com", "B", new List<string> { "reading" }));

        Assert.True(result.IsSuccess);
        Assert.Equal("https://b.example.com", result.Value!.Url);
        Assert.Equal("B", result.Value!.Title);
        Assert.Equal(new[] { "reading" }, result.Value!.Tags);
        Assert.Equal(originalCreatedAt, result.Value!.CreatedAt);
        Assert.True(result.Value!.UpdatedAt > originalCreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ToItsOwnUnchangedUrl_DoesNotFlagAsDuplicate()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        var added = await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", null));

        var result = await service.UpdateAsync(added.Value!.Id,
            new UpdateBookmarkRequest("https://a.example.com", "A Updated", null));

        Assert.True(result.IsSuccess);
        Assert.Equal("A Updated", result.Value!.Title);
    }

    [Fact]
    public async Task UpdateAsync_ToAnotherExistingBookmarksUrl_ReturnsDuplicateFailure()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", null));
        var second = await service.AddAsync(new AddBookmarkRequest("https://b.example.com", "B", null));

        var result = await service.UpdateAsync(second.Value!.Id,
            new UpdateBookmarkRequest("https://a.example.com", "B", null));

        Assert.False(result.IsSuccess);
        Assert.Equal("This URL is already saved.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenBookmarkDoesNotExist_ReturnsFailure()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateBookmarkRequest("https://a.example.com", "A", null));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingBookmark_ReturnsTrue()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);
        var added = await service.AddAsync(new AddBookmarkRequest("https://a.example.com", "A", null));

        var deleted = await service.DeleteAsync(added.Value!.Id);

        Assert.True(deleted);
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_WhenBookmarkDoesNotExist_ReturnsFalse()
    {
        var repository = new InMemoryBookmarkRepository();
        var service = CreateService(repository);

        var deleted = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }
}
