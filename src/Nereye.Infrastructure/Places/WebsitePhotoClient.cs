using System.Net;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class WebsitePhotoClient
{
    private static readonly HashSet<string> SkipHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "instagram.com", "www.instagram.com", "facebook.com", "www.facebook.com", "m.facebook.com",
        "twitter.com", "www.twitter.com", "x.com", "www.x.com", "maps.google.com", "www.google.com",
        "linktr.ee", "wa.me",
    };

    private readonly HttpClient _http;

    public WebsitePhotoClient(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(5);
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", HttpIdentity.UserAgent);
        }

        if (!_http.DefaultRequestHeaders.Accept.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html");
        }
    }

    public async Task<string?> FindAsync(string? website, CancellationToken cancellationToken = default)
    {
        if (!PlacePhoto.IsHttp(website) || !Uri.TryCreate(website, UriKind.Absolute, out var uri) || !IsPublicHttp(uri))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var media = response.Content.Headers.ContentType?.MediaType;
        if (media is not null
            && !media.Contains("html", StringComparison.OrdinalIgnoreCase)
            && !media.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var html = await ReadLimitedAsync(response.Content, 160_000, cancellationToken);
        var page = response.RequestMessage?.RequestUri?.ToString() ?? website!;
        return PlacePhoto.TryOgImage(html, page);
    }

    internal static bool IsPublicHttp(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https") || uri.IsLoopback || SkipHosts.Contains(uri.Host))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out _))
        {
            return false;
        }

        return uri.Host.Contains('.', StringComparison.Ordinal);
    }

    private static async Task<string> ReadLimitedAsync(HttpContent content, int maxChars, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[Math.Min(8192, maxChars)];
        var remaining = maxChars;
        var acc = new System.Text.StringBuilder(Math.Min(maxChars, 32_000));
        while (remaining > 0)
        {
            var n = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
            if (n == 0)
            {
                break;
            }

            acc.Append(buffer, 0, n);
            remaining -= n;
        }

        return acc.ToString();
    }
}
