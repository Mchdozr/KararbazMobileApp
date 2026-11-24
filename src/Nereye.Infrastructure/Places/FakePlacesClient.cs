using Nereye.Application.Places;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class FakePlacesClient : IPlacesClient, IRichPlaceDetails
{
    // Lazy: aşağıdaki statik sözlükler hazır olduktan sonra kurulur (alan başlatma sırasına bağlı kalmasın).
    private static readonly Lazy<IReadOnlyList<PlaceSnapshot>> Catalog = new(CreateIstanbul);

    public static IReadOnlyList<PlaceSnapshot> Istanbul => Catalog.Value;

    public Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var radius = SearchRadius.Clamp(radiusMeters);
        var typed = Istanbul.Where(p => IntentPlaceTypes.Fits(intent, p.Name, p.Cuisine, p.Types)).ToList();
        var nearby = typed
            .Where(p => GeoMath.HaversineMeters(latitude, longitude, p.Latitude, p.Longitude) <= radius)
            .OrderBy(p => GeoMath.HaversineMeters(latitude, longitude, p.Latitude, p.Longitude))
            .ToList();
        return Task.FromResult<IReadOnlyList<PlaceSnapshot>>(nearby);
    }

    public Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Istanbul.FirstOrDefault(p => p.PlaceId == placeId) is { } p
            // Kart düzeyi: zengin alanlar yalnızca RichDetailsAsync'te (Google'daki ayrımla aynı).
            ? p with { Summary = null, Reviews = null, Features = null }
            : null);

    public Task<PlaceSnapshot?> RichDetailsAsync(string placeId, CancellationToken cancellationToken = default) =>
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
            P("nereye:mini", "MiniMüzikhol", "Osmanağa, Kadıköy", 40.9902, 29.0271, ["night_club", "bar"], true, 4.3, Media("mini")),
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
                open ? OsmOpeningHours.Parse(HoursOf(types)) : [],
                attribution,
                Phone: "0216 555 00 " + (10 + id.Sum(c => c) % 89),
                // "Açık" bayrağı veri çekildiği ana aittir; sunumda bu metinden yeniden hesaplanır. Kapalı örnek
                // mekanın (Opera Kahve) saati yok: her zaman kapalı kalır, testler saate bağlı olmaz.
                OpeningHoursText: open ? HoursOf(types) : null,
                Summary: Summaries.GetValueOrDefault(id),
                PriceLevel: types.Contains("park") ? 0 : types.Contains("restaurant") || types.Contains("performing_arts_theater") ? 3 : 2,
                Category: CategoryOf(types),
                Photos: [photo],
                Features: FeaturesOf(types),
                Reviews: ReviewsOf(name));
    }

    // Geliştirme ve testlerde detay sayfası gerçek veriyle aynı dolulukta görünsün.
    private static readonly Dictionary<string, string> Summaries = new()
    {
        ["nereye:petra"] = "Kendi kavurduğu çekirdeklerle bilinen, yüksek tavanlı ve aydınlık üçüncü dalga kahve dükkanı.",
        ["nereye:fazil"] = "1923'ten beri közde Türk kahvesi pişiren, Kadıköy Çarşı'nın simge kahvecisi.",
        ["nereye:walter"] = "Laboratuvar temalı dekoruyla tanınan, kavurma ve demleme odaklı kahve dükkanı.",
        ["nereye:ciya"] = "Anadolu'nun unutulmaya yüz tutmuş yöresel yemeklerini sunan, ödüllü esnaf lokantası.",
        ["nereye:sureyya"] = "1927 yapımı tarihi binada opera, bale ve klasik müzik konserlerine ev sahipliği yapan sahne.",
        ["nereye:modern"] = "Türkiye'nin ilk modern ve çağdaş sanat müzesi; Boğaz manzaralı sergi salonları.",
        ["nereye:moda"] = "Deniz kenarında yürüyüş yolu, çimenlik alanlar ve gün batımı manzarası sunan sahil parkı.",
    };

    // Türe göre gerçekçi saatler: gece mekanı gece yarısını aşar, park her zaman açık.
    private static string HoursOf(string[] types) => types[0] switch
    {
        "night_club" or "bar" => "We-Sa 21:00-04:00; Su 21:00-02:00",
        "park" => "24/7",
        "restaurant" => "Mo-Su 11:00-23:30",
        "performing_arts_theater" => "Tu-Su 11:00-22:30; Mo off",
        "museum" => "Tu-Su 10:00-18:00; Mo off",
        _ => "Mo-Th 08:00-23:00; Fr-Sa 08:00-24:00; Su 09:00-23:00",
    };

    private static string CategoryOf(string[] types) => types[0] switch
    {
        "cafe" or "coffee_shop" => "Kahve dükkanı",
        "restaurant" => "Restoran",
        "performing_arts_theater" => "Sahne sanatları",
        "museum" => "Müze",
        "night_club" or "bar" => "Gece kulübü",
        "park" => "Park",
        _ => "Mekan"
    };

    private static string[] FeaturesOf(string[] types) => types[0] switch
    {
        "cafe" or "coffee_shop" => ["Açık alan", "Kahve", "Tatlı", "Paket servis", "Kart geçerli", "Tuvalet"],
        "restaurant" => ["Rezervasyon", "Vejetaryen seçenek", "Gruplara uygun", "Mekanda servis", "Kart geçerli"],
        "park" => ["Açık alan", "Çocuk dostu", "Evcil hayvan dostu"],
        "night_club" or "bar" => ["Canlı müzik", "Alkollü içecek", "Gruplara uygun"],
        _ => ["Gruplara uygun", "Tekerlekli sandalyeye uygun", "Tuvalet"]
    };

    private static PlaceReview[] ReviewsOf(string name) =>
    [
        new("Elif K.", 5, $"{name} beklediğimden de iyiydi. Personel ilgili, ortam ferah; hafta içi öğleden sonra çok sakin.", "2 hafta önce"),
        new("Mert A.", 4, "Genel olarak memnun kaldım. Hafta sonu kalabalık oluyor, erken gitmekte fayda var.", "1 ay önce"),
        new("Zeynep D.", 5, "Semtin en sevdiğim yerlerinden. Tekrar geleceğim.", "3 ay önce"),
    ];
}
