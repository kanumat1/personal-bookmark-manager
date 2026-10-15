using BookmarkManager.Api.Endpoints;
using BookmarkManager.Core.Repositories;
using BookmarkManager.Core.Services;
using BookmarkManager.Persistence;

var builder = WebApplication.CreateBuilder(args);

var bookmarksFilePath = builder.Configuration["BookmarksFilePath"] ?? "bookmarks.json";
builder.Services.AddSingleton<IBookmarkRepository>(_ => new JsonBookmarkRepository(bookmarksFilePath));
builder.Services.AddHttpClient<ITitlePageFetcher, HttpTitlePageFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<TitleFetchService>();
builder.Services.AddScoped<BookmarkService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapBookmarkEndpoints();

app.Run();
