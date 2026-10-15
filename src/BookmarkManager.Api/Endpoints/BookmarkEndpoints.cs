using BookmarkManager.Core.Services;

namespace BookmarkManager.Api.Endpoints;

public static class BookmarkEndpoints
{
    public static void MapBookmarkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bookmarks");

        group.MapGet("/", async (string? tag, bool? untagged, string? search, BookmarkService bookmarkService,
            CancellationToken cancellationToken) =>
        {
            var bookmarks = await bookmarkService.GetAllAsync(tag, untagged ?? false, search, cancellationToken);
            return Results.Ok(bookmarks);
        });

        group.MapPost("/", async (AddBookmarkRequest request, BookmarkService bookmarkService, CancellationToken cancellationToken) =>
        {
            var result = await bookmarkService.AddAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/bookmarks/{result.Value!.Id}", result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateBookmarkRequest request, BookmarkService bookmarkService,
            CancellationToken cancellationToken) =>
        {
            var result = await bookmarkService.UpdateAsync(id, request, cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error == "Bookmark not found."
                ? Results.NotFound(new { error = result.Error })
                : Results.BadRequest(new { error = result.Error });
        });

        group.MapDelete("/{id:guid}", async (Guid id, BookmarkService bookmarkService, CancellationToken cancellationToken) =>
        {
            var deleted = await bookmarkService.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound(new { error = "Bookmark not found." });
        });

        app.MapGet("/api/tags", async (BookmarkService bookmarkService, CancellationToken cancellationToken) =>
        {
            var tags = await bookmarkService.GetAllTagsAsync(cancellationToken);
            return Results.Ok(tags);
        });
    }
}
