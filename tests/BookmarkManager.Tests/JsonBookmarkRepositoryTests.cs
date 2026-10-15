using BookmarkManager.Core.Models;
using BookmarkManager.Persistence;

namespace BookmarkManager.Tests;

public class JsonBookmarkRepositoryTests : IDisposable
{
    private readonly string _filePath;

    public JsonBookmarkRepositoryTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"bookmarks-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    private static Bookmark CreateBookmark(string url = "https://example.com") => new()
    {
        Id = Guid.NewGuid(),
        Url = url,
        NormalizedUrl = url,
        Title = "Example",
        Tags = new List<string> { "test" },
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task GetAllAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        var repository = new JsonBookmarkRepository(_filePath);

        var result = await repository.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task AddAsync_PersistsBookmarkToDisk()
    {
        var repository = new JsonBookmarkRepository(_filePath);
        var bookmark = CreateBookmark();

        await repository.AddAsync(bookmark);

        Assert.True(File.Exists(_filePath));
        var all = await repository.GetAllAsync();
        Assert.Single(all);
        Assert.Equal(bookmark.Id, all[0].Id);
    }

    [Fact]
    public async Task AddAsync_ThenNewRepositoryInstance_ReloadsFromDisk()
    {
        var repository = new JsonBookmarkRepository(_filePath);
        var bookmark = CreateBookmark();
        await repository.AddAsync(bookmark);

        var reloaded = new JsonBookmarkRepository(_filePath);
        var all = await reloaded.GetAllAsync();

        Assert.Single(all);
        Assert.Equal(bookmark.Url, all[0].Url);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesExistingBookmark()
    {
        var repository = new JsonBookmarkRepository(_filePath);
        var bookmark = CreateBookmark();
        await repository.AddAsync(bookmark);

        bookmark.Title = "Updated Title";
        await repository.UpdateAsync(bookmark);

        var stored = await repository.GetByIdAsync(bookmark.Id);
        Assert.NotNull(stored);
        Assert.Equal("Updated Title", stored!.Title);
    }

    [Fact]
    public async Task UpdateAsync_WhenBookmarkDoesNotExist_Throws()
    {
        var repository = new JsonBookmarkRepository(_filePath);
        var bookmark = CreateBookmark();

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(bookmark));
    }

    [Fact]
    public async Task DeleteAsync_RemovesBookmark()
    {
        var repository = new JsonBookmarkRepository(_filePath);
        var bookmark = CreateBookmark();
        await repository.AddAsync(bookmark);

        await repository.DeleteAsync(bookmark.Id);

        var all = await repository.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task SaveNoLockAsync_DoesNotLeaveTempFileBehind()
    {
        var repository = new JsonBookmarkRepository(_filePath);
        await repository.AddAsync(CreateBookmark());

        Assert.False(File.Exists(_filePath + ".tmp"));
    }
}
