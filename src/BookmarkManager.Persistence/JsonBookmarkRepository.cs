using System.Text.Json;
using BookmarkManager.Core.Models;
using BookmarkManager.Core.Repositories;

namespace BookmarkManager.Persistence;

/// <summary>
/// In-memory cache backed by a JSON flat file. Registered as a singleton so the app has exactly
/// one in-memory list and one <see cref="SemaphoreSlim"/>, which the atomic-write guarantee depends on.
/// </summary>
public class JsonBookmarkRepository : IBookmarkRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Bookmark>? _cache;

    public JsonBookmarkRepository(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<Bookmark>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var bookmarks = await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return bookmarks.ToList();
    }

    public async Task<Bookmark?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bookmarks = await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return bookmarks.FirstOrDefault(b => b.Id == id);
    }

    public async Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bookmarks = await EnsureLoadedNoLockAsync(cancellationToken).ConfigureAwait(false);
            bookmarks.Add(bookmark);
            await SaveNoLockAsync(bookmarks, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bookmarks = await EnsureLoadedNoLockAsync(cancellationToken).ConfigureAwait(false);
            var index = bookmarks.FindIndex(b => b.Id == bookmark.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"Bookmark '{bookmark.Id}' does not exist.");
            }

            bookmarks[index] = bookmark;
            await SaveNoLockAsync(bookmarks, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bookmarks = await EnsureLoadedNoLockAsync(cancellationToken).ConfigureAwait(false);
            bookmarks.RemoveAll(b => b.Id == id);
            await SaveNoLockAsync(bookmarks, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<Bookmark>> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await EnsureLoadedNoLockAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<Bookmark>> EnsureLoadedNoLockAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_filePath))
        {
            _cache = new List<Bookmark>();
            return _cache;
        }

        await using var stream = File.OpenRead(_filePath);
        var loaded = await JsonSerializer.DeserializeAsync<List<Bookmark>>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        _cache = loaded ?? new List<Bookmark>();
        return _cache;
    }

    /// <summary>Write-to-temp-then-rename so a crash mid-write cannot corrupt the existing file.</summary>
    private async Task SaveNoLockAsync(List<Bookmark> bookmarks, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFilePath = _filePath + ".tmp";
        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, bookmarks, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempFilePath, _filePath, overwrite: true);
        _cache = bookmarks;
    }
}
