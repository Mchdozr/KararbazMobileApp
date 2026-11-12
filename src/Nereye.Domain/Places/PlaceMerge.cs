using Nereye.Domain.Geo;

namespace Nereye.Domain.Places;

/// <summary>OSM kaydı ile Google kaydının aynı mekan olup olmadığına karar verir ve ikisini birleştirir.</summary>
public static class PlaceMerge
{
    public const double MatchMeters = 90;

    private static readonly string[] Generic = ["coffeeshop", "coffee", "kahve", "cafe", "kafe", "restaurant", "restoran"];

    public static bool Same(PlaceSnapshot a, PlaceSnapshot b, double maxMeters = MatchMeters) =>
        GeoMath.HaversineMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude) <= maxMeters && SameName(a.Name, b.Name);

    public static bool SameName(string first, string second)
    {
        var a = Norm(first);
        var b = Norm(second);
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        return a == b
               || (a.Length >= 4 && b.Contains(a, StringComparison.Ordinal))
               || (b.Length >= 4 && a.Contains(b, StringComparison.Ordinal));
    }

    /// <summary>
    /// Kimlik, ad ve konum OSM'den kalır (katalog odur); OSM'de boş olan her alan Google'dan dolar.
    /// Açık/kapalı ve puan her zaman Google'dan gelir: OSM'de bu bilgi ya yoktur ya eskidir.
    /// </summary>
    public static PlaceSnapshot Overlay(PlaceSnapshot osm, PlaceSnapshot google) => osm with
    {
        Address = Blank(osm.Address) ? google.Address : osm.Address,
        Phone = Blank(osm.Phone) ? google.Phone : osm.Phone,
        Website = Blank(osm.Website) ? google.Website : osm.Website,
        OpeningHoursText = Blank(osm.OpeningHoursText) ? google.OpeningHoursText : osm.OpeningHoursText,
        PhotoReference = Blank(osm.PhotoReference) ? google.PhotoReference : osm.PhotoReference,
        GoogleMapsUri = google.GoogleMapsUri ?? osm.GoogleMapsUri,
        OpenNow = google.OpenNow ?? osm.OpenNow,
        GoogleRating = google.GoogleRating ?? osm.GoogleRating,
        GoogleUserRatingCount = google.GoogleUserRatingCount ?? osm.GoogleUserRatingCount,
        Summary = google.Summary ?? osm.Summary,
        PriceLevel = google.PriceLevel ?? osm.PriceLevel,
        Category = google.Category ?? osm.Category,
        Photos = google.Photos ?? osm.Photos,
        Features = google.Features ?? osm.Features,
        Reviews = google.Reviews ?? osm.Reviews,
        Attribution = "Harita verisi: OpenStreetMap · Google"
    };

    /// <summary>Adaylar içinden aynı mekanı seçer; emin değilse null (yanlış mekanın yorumunu göstermektense boş kalsın).</summary>
    public static PlaceSnapshot? Pick(PlaceSnapshot target, IEnumerable<PlaceSnapshot> candidates, double maxMeters = 120) =>
        candidates
            .Where(c => Same(target, c, maxMeters))
            .OrderBy(c => GeoMath.HaversineMeters(target.Latitude, target.Longitude, c.Latitude, c.Longitude))
            .FirstOrDefault();

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static string Norm(string name)
    {
        var lower = name.ToLowerInvariant()
            .Replace('ı', 'i').Replace('İ', 'i').Replace('ş', 's').Replace('ğ', 'g')
            .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');
        var flat = new string(lower.Where(char.IsLetterOrDigit).ToArray());
        foreach (var drop in Generic)
        {
            flat = flat.Replace(drop, "", StringComparison.Ordinal);
        }

        return flat;
    }
}
