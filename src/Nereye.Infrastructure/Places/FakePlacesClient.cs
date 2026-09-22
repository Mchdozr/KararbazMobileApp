using Nereye.Application.Places;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class FakePlacesClient : IPlacesClient
{
    public static readonly IReadOnlyList<PlaceSnapshot> Istanbul = CreateIstanbul();

    public Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var types = IntentPlaceTypes.GoogleTypes(intent);
        var radius = SearchRadius.Clamp(radiusMeters);
        var typed = Istanbul.Where(p => p.Types.Any(t => types.Contains(t))).ToList();
        var nearby = typed
            .Where(p => GeoMath.HaversineMeters(latitude, longitude, p.Latitude, p.Longitude) <= radius)
            .OrderBy(p => GeoMath.HaversineMeters(latitude, longitude, p.Latitude, p.Longitude))
            .ToList();
        return Task.FromResult<IReadOnlyList<PlaceSnapshot>>(nearby);
    }

    public Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Istanbul.FirstOrDefault(p => p.PlaceId == placeId));

    public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        IReadOnlyList<PlaceSnapshot> result = Istanbul
            .Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (p.Address?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        return Task.FromResult(result);
    }

    private static IReadOnlyList<PlaceSnapshot> CreateIstanbul()
    {
        var attribution = "Harita verisi: Google";
        return
        [
            P("nereye:petra", "Petra Roasting Co.", "Caferağa, Kadıköy", 40.9880, 29.0244, ["cafe", "coffee_shop"], true, 4.6, Media("petra")),
            P("nereye:fazil", "Fazıl Bey Türk Kahvesi", "Caferağa, Kadıköy", 40.9901, 29.0228, ["cafe", "coffee_shop"], true, 4.4, Media("fazil")),
            P("nereye:walter", "Walter's Coffee Roastery", "Caferağa, Kadıköy", 40.9872, 29.0268, ["cafe", "coffee_shop"], true, 4.5, Media("walter")),
            P("nereye:montag", "Montag Coffee", "Osmanağa, Kadıköy", 40.9914, 29.0279, ["cafe", "coffee_shop"], true, 4.3, Media("montag")),
            P("nereye:federica", "Federica", "Caferağa, Kadıköy", 40.9865, 29.0235, ["cafe", "coffee_shop"], true, 4.4, Media("federica")),
            P("nereye:goygoy", "Goygoy Kahve", "Moda, Kadıköy", 40.9848, 29.0255, ["cafe", "coffee_shop"], true, 4.2, Media("goygoy")),
            P("nereye:opera", "Opera Kahve", "Caferağa, Kadıköy", 40.9878, 29.0291, ["cafe", "coffee_shop"], false, 4.1, Media("opera")),
            P("nereye:mandabatmaz", "Mandabatmaz", "Olivia Geçidi, Beyoğlu", 41.0332, 28.9784, ["cafe", "coffee_shop"], true, 4.7, Media("mandabatmaz")),
            P("nereye:kronotrop", "Kronotrop Cihangir", "Cihangir, Beyoğlu", 41.0324, 28.9865, ["cafe", "coffee_shop"], true, 4.5, Media("kronotrop")),
            P("nereye:ciya", "Çiya Sofrası", "Caferağa, Kadıköy", 40.9896, 29.0258, ["restaurant"], true, 4.5, Media("ciya")),
            P("nereye:karakoy", "Karaköy Lokantası", "Kemankeş, Beyoğlu", 41.0234, 28.9799, ["restaurant"], true, 4.4, Media("karakoy")),
            P("nereye:sureyya", "Süreyya Operası", "Caferağa, Kadıköy", 40.9875, 29.0289, ["performing_arts_theater"], true, 4.8, Media("sureyya")),
            P("nereye:zorlu", "Zorlu PSM", "Levazım, Beşiktaş", 41.0663, 29.0165, ["performing_arts_theater"], true, 4.6, Media("zorlu")),
            P("nereye:modern", "İstanbul Modern", "Kılıçali Paşa, Beyoğlu", 41.0267, 28.9833, ["museum"], true, 4.5, Media("modern")),
            P("nereye:mini", "MiniMüzikhol", "Osmanağa, Kadıköy", 40.9902, 29.0271, ["night_club", "bar"], false, 4.3, Media("mini")),
            P("nereye:moda", "Moda Sahil Parkı", "Moda, Kadıköy", 40.9808, 29.0261, ["park"], true, 4.7, Media("moda")),
        ];

        static string Media(string id) => $"/media/venues/{id}.jpg";

        PlaceSnapshot P(
            string id,
            string name,
            string address,
            double lat,
            double lng,
            string[] types,
            bool open,
            double rating,
            string photo) =>
            new(
                id,
                name,
                address,
                lat,
                lng,
                types,
                open,
                photo,
                rating,
                120,
                $"https://www.google.com/maps/search/?api=1&query={lat},{lng}",
                [
                    new PlaceHour(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(22, 0)),
                    new PlaceHour(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(22, 0)),
                    new PlaceHour(DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(22, 0)),
                    new PlaceHour(DayOfWeek.Thursday, new TimeOnly(10, 0), new TimeOnly(22, 0)),
                    new PlaceHour(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(23, 0)),
                    new PlaceHour(DayOfWeek.Saturday, new TimeOnly(10, 0), new TimeOnly(23, 0)),
                    new PlaceHour(DayOfWeek.Sunday, new TimeOnly(11, 0), new TimeOnly(21, 0))
                ],
                attribution);
    }
}
