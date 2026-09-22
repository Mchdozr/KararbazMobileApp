using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class MapillaryPhotoClient
{
    private readonly HttpClient _http;
    private readonly string? _token;

    public MapillaryPhotoClient(HttpClient http, IOptions<MapillaryOptions> options)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(8);
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Nereye/1.0 (https://nereye.app)");
        }

        _token = string.IsNullOrWhiteSpace(options.Value.Token)
            ? Environment.GetEnvironmentVariable("MAPILLARY_TOKEN")
            : options.Value.Token;
    }

    public async Task<string?> FindAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return null;
        }

        var lat = latitude.ToString("0.######", CultureInfo.InvariantCulture);
        var lng = longitude.ToString("0.######", CultureInfo.InvariantCulture);
        var path =
            $"https://graph.mapillary.com/images?access_token={Uri.EscapeDataString(_token)}&fields=id,thumb_1024_url&closeto={lng},{lat}&radius=80";
        using var response = await _http.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return MapillaryPhoto.Parse(json);
    }
}
