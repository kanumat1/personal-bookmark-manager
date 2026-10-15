using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using BookmarkManager.Core.Common;

namespace BookmarkManager.Core.Services;

/// <summary>
/// Fetches a remote page's &lt;title&gt; with SSRF protection (rejects loopback/private/link-local hosts,
/// including redirect targets), a bounded timeout, and a capped response size.
/// </summary>
public class HttpTitlePageFetcher : ITitlePageFetcher
{
    private const int MaxResponseBytes = 1024 * 1024; // 1 MB
    private const int MaxRedirects = 5;
    private static readonly Regex TitleRegex = new(@"<title[^>]*>\s*(.*?)\s*</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;

    public HttpTitlePageFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<string>> FetchTitleAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var currentUrl = url;

        for (var redirectCount = 0; redirectCount <= MaxRedirects; redirectCount++)
        {
            var hostCheck = await EnsureHostIsAllowedAsync(currentUrl, cancellationToken).ConfigureAwait(false);
            if (!hostCheck.IsSuccess)
            {
                return Result<string>.Failure(hostCheck.Error!);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return Result<string>.Failure("Could not reach that URL.");
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
                {
                    currentUrl = new Uri(currentUrl, response.Headers.Location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Result<string>.Failure($"The page returned status {(int)response.StatusCode}.");
                }

                if (!IsHtml(response.Content.Headers.ContentType))
                {
                    return Result<string>.Failure("The page did not return an HTML title.");
                }

                if (response.Content.Headers.ContentLength is { } contentLength && contentLength > MaxResponseBytes)
                {
                    return Result<string>.Failure("The page response was too large.");
                }

                var html = await ReadCappedContentAsync(response, cancellationToken).ConfigureAwait(false);
                var match = TitleRegex.Match(html);
                if (!match.Success)
                {
                    return Result<string>.Failure("No title was found on that page.");
                }

                var title = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                return string.IsNullOrWhiteSpace(title)
                    ? Result<string>.Failure("No title was found on that page.")
                    : Result<string>.Success(title);
            }
        }

        return Result<string>.Failure("Too many redirects.");
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static bool IsHtml(MediaTypeHeaderValue? contentType) =>
        contentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<string> ReadCappedContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[MaxResponseBytes];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken)
                   .ConfigureAwait(false)) > 0)
        {
            totalRead += read;
        }

        return System.Text.Encoding.UTF8.GetString(buffer, 0, totalRead);
    }

    private static async Task<Result<bool>> EnsureHostIsAllowedAsync(Uri url, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(url.Host, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return Result<bool>.Failure("Could not resolve that host.");
        }

        if (addresses.Length == 0 || addresses.Any(IsPrivateOrLoopback))
        {
            return Result<bool>.Failure("That URL points to a disallowed address.");
        }

        return Result<bool>.Success(true);
    }

    private static bool IsPrivateOrLoopback(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true,
                172 when bytes[1] is >= 16 and <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false
            };
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            // fc00::/7 (unique local addresses)
            return (bytes[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}
