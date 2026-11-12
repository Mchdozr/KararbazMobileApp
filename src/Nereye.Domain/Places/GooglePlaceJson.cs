using System.Text.Json;

namespace Nereye.Domain.Places;

/// <summary>Google Places (New) yanıtını <see cref="PlaceSnapshot"/>'a çevirir. Ağ yok; saf ve test edilebilir.</summary>
public static class GooglePlaceJson
{
    public const int MaxPhotos = 5;
    public const int MaxReviews = 5;
    public const int MaxReviewLength = 600;

    /// <summary>Liste istekleri: kart için yeten alanlar (Enterprise SKU; Atmosphere alanı yok).</summary>
    public const string ListMask =
        "places.id,places.displayName,places.formattedAddress,places.shortFormattedAddress,places.location,places.types,"
        + "places.primaryTypeDisplayName,places.photos.name,places.nationalPhoneNumber,places.websiteUri,places.rating,"
        + "places.userRatingCount,places.priceLevel,places.googleMapsUri,places.currentOpeningHours.openNow,"
        + "places.regularOpeningHours.openNow,places.regularOpeningHours.weekdayDescriptions";

    private const string RichFields =
        "id,displayName,formattedAddress,shortFormattedAddress,location,types,primaryTypeDisplayName,photos.name,"
        + "nationalPhoneNumber,websiteUri,rating,userRatingCount,priceLevel,googleMapsUri,currentOpeningHours.openNow,"
        + "regularOpeningHours.openNow,regularOpeningHours.weekdayDescriptions,editorialSummary,reviews.rating,reviews.text,"
        + "reviews.relativePublishTimeDescription,reviews.authorAttribution,dineIn,takeout,delivery,reservable,outdoorSeating,"
        + "liveMusic,servesBreakfast,servesBrunch,servesCoffee,servesDessert,servesBeer,servesWine,servesCocktails,"
        + "servesVegetarianFood,goodForChildren,goodForGroups,allowsDogs,restroom,accessibilityOptions,paymentOptions,parkingOptions";

    /// <summary>Tek mekan detayı: özet, yorumlar ve olanaklar dahil.</summary>
    public const string DetailMask = RichFields;

    /// <summary>Metin aramasıyla tek mekanı zengin içerikle bulmak için.</summary>
    public static readonly string SearchRichMask =
        string.Join(',', RichFields.Split(',').Select(f => "places." + f));

    // Sıra önemli: ekranda bu sırayla, en ayırt edici olan önce görünür.
    private static readonly (string Key, string Label)[] FeatureFlags =
    [
        ("outdoorSeating", "Açık alan"),
        ("reservable", "Rezervasyon"),
        ("servesBreakfast", "Kahvaltı"),
        ("servesBrunch", "Brunch"),
        ("servesCoffee", "Kahve"),
        ("servesDessert", "Tatlı"),
        ("servesVegetarianFood", "Vejetaryen seçenek"),
        ("liveMusic", "Canlı müzik"),
        ("goodForGroups", "Gruplara uygun"),
        ("goodForChildren", "Çocuk dostu"),
        ("allowsDogs", "Evcil hayvan dostu"),
        ("takeout", "Paket servis"),
        ("delivery", "Eve teslimat"),
        ("dineIn", "Mekanda servis"),
        ("restroom", "Tuvalet"),
    ];

    public static IReadOnlyList<PlaceSnapshot> ParseList(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty("places", out var places)
            || places.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<PlaceSnapshot>();
        foreach (var el in places.EnumerateArray())
        {
            if (Parse(el) is { } place)
            {
                list.Add(place);
            }
        }

        return list;
    }

    public static PlaceSnapshot? Parse(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = Str(p, "id") ?? Str(p, "name")?.Replace("places/", "", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var lat = 0d;
        var lng = 0d;
        if (p.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object)
        {
            lat = Num(loc, "latitude") ?? 0;
            lng = Num(loc, "longitude") ?? 0;
        }

        var types = p.TryGetProperty("types", out var typesEl) && typesEl.ValueKind == JsonValueKind.Array
            ? typesEl.EnumerateArray().Select(t => t.GetString() ?? "").Where(s => s.Length > 0).ToList()
            : [];

        var photos = Photos(p);
        var features = Features(p);
        var reviews = Reviews(p);
        var count = Num(p, "userRatingCount");

        return new PlaceSnapshot(
            id,
            Text(p, "displayName") ?? id,
            Str(p, "shortFormattedAddress") ?? Str(p, "formattedAddress"),
            lat,
            lng,
            types,
            OpenNow(p),
            photos.Count > 0 ? photos[0] : null,
            Num(p, "rating"),
            count is null ? null : (int)count.Value,
            Str(p, "googleMapsUri"),
            [],
            "Harita verisi: Google",
            Str(p, "websiteUri"),
            null,
            Str(p, "nationalPhoneNumber"),
            WeekdayText(p),
            Summary: Text(p, "editorialSummary"),
            PriceLevel: Price(Str(p, "priceLevel")),
            Category: Capitalize(Text(p, "primaryTypeDisplayName")),
            Photos: photos.Count > 0 ? photos : null,
            Features: features.Count > 0 ? features : null,
            Reviews: reviews.Count > 0 ? reviews : null);
    }

    public static int? Price(string? level) => level switch
    {
        "PRICE_LEVEL_FREE" => 0,
        "PRICE_LEVEL_INEXPENSIVE" => 1,
        "PRICE_LEVEL_MODERATE" => 2,
        "PRICE_LEVEL_EXPENSIVE" => 3,
        "PRICE_LEVEL_VERY_EXPENSIVE" => 4,
        _ => null
    };

    private static List<string> Photos(JsonElement p)
    {
        var list = new List<string>();
        if (!p.TryGetProperty("photos", out var photos) || photos.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var photo in photos.EnumerateArray())
        {
            var name = Str(photo, "name");
            if (name is not null && name.StartsWith("places/", StringComparison.Ordinal))
            {
                list.Add(name);
                if (list.Count == MaxPhotos)
                {
                    break;
                }
            }
        }

        return list;
    }

    private static List<string> Features(JsonElement p)
    {
        var list = new List<string>();
        foreach (var (key, label) in FeatureFlags)
        {
            if (Flag(p, key))
            {
                list.Add(label);
            }
        }

        if (Flag(p, "servesBeer") || Flag(p, "servesWine") || Flag(p, "servesCocktails"))
        {
            list.Add("Alkollü içecek");
        }

        if (p.TryGetProperty("accessibilityOptions", out var access) && Flag(access, "wheelchairAccessibleEntrance"))
        {
            list.Add("Tekerlekli sandalyeye uygun");
        }

        if (p.TryGetProperty("paymentOptions", out var pay) && (Flag(pay, "acceptsCreditCards") || Flag(pay, "acceptsDebitCards")))
        {
            list.Add("Kart geçerli");
        }

        if (p.TryGetProperty("parkingOptions", out var park)
            && park.ValueKind == JsonValueKind.Object
            && park.EnumerateObject().Any(o => o.Value.ValueKind == JsonValueKind.True))
        {
            list.Add("Otopark");
        }

        return list;
    }

    private static List<PlaceReview> Reviews(JsonElement p)
    {
        var list = new List<PlaceReview>();
        if (!p.TryGetProperty("reviews", out var reviews) || reviews.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var r in reviews.EnumerateArray())
        {
            var text = Text(r, "text")?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (text.Length > MaxReviewLength)
            {
                text = text[..MaxReviewLength].TrimEnd() + "…";
            }

            string? author = null;
            string? photo = null;
            if (r.TryGetProperty("authorAttribution", out var who) && who.ValueKind == JsonValueKind.Object)
            {
                author = Str(who, "displayName");
                photo = Str(who, "photoUri");
            }

            list.Add(new PlaceReview(
                string.IsNullOrWhiteSpace(author) ? "Google kullanıcısı" : author,
                Math.Clamp((int)Math.Round(Num(r, "rating") ?? 0), 0, 5),
                text,
                Str(r, "relativePublishTimeDescription"),
                photo is not null && photo.StartsWith("https://", StringComparison.Ordinal) ? photo : null));
            if (list.Count == MaxReviews)
            {
                break;
            }
        }

        return list;
    }

    private static string? WeekdayText(JsonElement p)
    {
        if (!p.TryGetProperty("regularOpeningHours", out var hours)
            || !hours.TryGetProperty("weekdayDescriptions", out var days)
            || days.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var text = string.Join("; ", days.EnumerateArray().Select(d => d.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)));
        return text.Length == 0 ? null : text;
    }

    private static bool? OpenNow(JsonElement p)
    {
        foreach (var key in (string[])["currentOpeningHours", "regularOpeningHours"])
        {
            if (p.TryGetProperty(key, out var hours)
                && hours.ValueKind == JsonValueKind.Object
                && hours.TryGetProperty("openNow", out var open)
                && open.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return open.GetBoolean();
            }
        }

        return null;
    }

    private static string? Capitalize(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : char.ToUpper(text[0], new System.Globalization.CultureInfo("tr-TR")) + text[1..];
    }

    private static bool Flag(JsonElement p, string key) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? Str(JsonElement p, string key) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static double? Num(JsonElement p, string key) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : null;

    /// <summary><c>{ "text": "…" }</c> biçimli yerelleştirilmiş metin alanı.</summary>
    private static string? Text(JsonElement p, string key) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(key, out var v) ? Str(v, "text") : null;
}
