namespace Nereye.Infrastructure.Places;

/// <summary>
/// Rota sunucuları (<c>Routing</c>). Kendi OSRM'imiz kurulunca yaya ve araç ayrı örneklerde çalışır
/// (her osrm-routed tek profil taşır); bkz. docs/kendi-sunucular.md. Boş ya da geçersiz adres genel sunucuya düşer.
/// </summary>
public sealed class RoutingOptions
{
    public const string Section = "Routing";
    public const string PublicOsrm = "https://router.project-osrm.org/";

    public string? FootBaseUrl { get; set; }
    public string? CarBaseUrl { get; set; }

    public Uri FootBase => EndpointUrl.Base(FootBaseUrl) ?? new Uri(PublicOsrm);
    public Uri CarBase => EndpointUrl.Base(CarBaseUrl) ?? new Uri(PublicOsrm);
}

/// <summary>
/// Overpass uçları (<c>Overpass:Endpoints</c>), deneme sırasıyla. Kendi sunucumuz ilk sıraya, genel uçlar yedeğe
/// konur. Liste boşsa genel uçlar kullanılır.
/// </summary>
public sealed class OverpassOptions
{
    public const string Section = "Overpass";

    public static readonly IReadOnlyList<Uri> PublicEndpoints =
    [
        new("https://overpass-api.de/api/interpreter"),
        new("https://overpass.kumi.systems/api/interpreter"),
        new("https://overpass.osm.ch/api/interpreter")
    ];

    public List<string> Endpoints { get; set; } = [];

    public IReadOnlyList<Uri> EffectiveEndpoints
    {
        get
        {
            var valid = Endpoints.Select(EndpointUrl.Absolute).OfType<Uri>().ToList();
            return valid.Count > 0 ? valid : PublicEndpoints;
        }
    }
}

internal static class EndpointUrl
{
    public static Uri? Absolute(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;

    /// <summary>Taban adres "/" ile biter: göreli yol (route/v1/...) üstüne yazılmasın.</summary>
    public static Uri? Base(string? value) =>
        Absolute(value) is { } uri ? new Uri(uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : uri.AbsoluteUri + "/") : null;
}
