namespace Nereye.Infrastructure.Places;

/// <summary>
/// Dış servislere (Nominatim, Overpass, OSRM, Wikimedia…) giden isteklerin User-Agent'ı.
/// OSM politikaları ulaşılabilir bir iletişim bilgisi ister. Bu bilgi kodda uydurulmaz:
/// `Places:Contact` ayarından (ör. bir e-posta ya da API alan adı) gelir; yoksa yalnızca uygulama adı gider.
/// </summary>
public static class HttpIdentity
{
    public static string UserAgent { get; private set; } = "Kararbaz/1.0";

    public static void Configure(string? contact)
    {
        var value = contact?.Trim();
        UserAgent = string.IsNullOrEmpty(value) || value.Any(char.IsControl) || value.Contains(')')
            ? "Kararbaz/1.0"
            : $"Kararbaz/1.0 ({value})";
    }
}
