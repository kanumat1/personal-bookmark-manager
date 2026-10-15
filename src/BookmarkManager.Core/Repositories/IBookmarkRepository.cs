using BookmarkManager.Core.Models;

namespace BookmarkManager.Core.Repositories;

public interface IBookmarkRepository
{
    Task<IReadOnlyList<Bookmark>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Bookmark?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default);

    Task UpdateAsync(Bookmark bookmark, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
