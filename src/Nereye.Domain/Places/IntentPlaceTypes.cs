namespace Nereye.Domain.Places;

/// <summary>
/// Hangi mekan hangi niyete girer? Karar mekanın <b>ana türüne</b> göre verilir.
/// Google bir mekanı çok sayıda türle etiketler (bir Kore restoranı "bar", bir sokak lezzeti
/// restoranı "tourist_attraction" da olabilir). Herhangi bir türe bakmak, Kültür'de yemek mekanı,
/// Açık hava'da cami ya da restoran görmeye yol açar. Bu yüzden <c>PlaceSnapshot.Types[0]</c>
/// her kaynakta ana türdür (Google'da primaryType, OSM'de etiketten türetilen tür).
/// </summary>
public static class IntentPlaceTypes
{
    /// <summary>Google Nearby isteğinde aday toplamak için. Kesin eleme <see cref="Fits"/> ile yapılır.</summary>
    public static IReadOnlyList<string> GoogleTypes(Intent intent) => intent switch
    {
        Intent.Kahve => ["cafe", "coffee_shop"],
        Intent.Yemek => ["restaurant", "meal_takeaway"],
        Intent.Eglence => ["bar", "night_club", "bowling_alley", "movie_theater"],
        Intent.Kultur => ["performing_arts_theater", "movie_theater", "museum"],
        Intent.AcikHava => ["park", "tourist_attraction"],
        Intent.Gece => ["night_club", "bar"],
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, null)
    };

    public static IReadOnlyList<(string Key, string Value)> OsmTags(Intent intent) => intent switch
    {
        // Fırın ve dondurmacı "kahve" değildir; Kahve listesine girmesinler.
        Intent.Kahve => [("amenity", "cafe"), ("shop", "coffee")],
        Intent.Yemek => [("amenity", "restaurant"), ("amenity", "fast_food"), ("amenity", "food_court")],
        Intent.Eglence => [("amenity", "bar"), ("amenity", "pub"), ("amenity", "cinema"), ("amenity", "nightclub")],
        Intent.Kultur => [("amenity", "theatre"), ("amenity", "museum"), ("tourism", "museum"), ("tourism", "gallery"), ("amenity", "arts_centre"), ("amenity", "library")],
        Intent.AcikHava => [("leisure", "park"), ("leisure", "garden"), ("leisure", "playground"), ("leisure", "nature_reserve"), ("amenity", "park")],
        Intent.Gece => [("amenity", "bar"), ("amenity", "pub"), ("amenity", "nightclub")],
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, null)
    };

    public enum Family
    {
        Other,
        Food,
        Cafe,
        Bar,
        Nightlife,
        Show,
        Culture,
        Outdoor
    }

    private static readonly HashSet<string> CafeTypes =
        ["cafe", "coffee_shop", "tea_house", "cat_cafe", "dog_cafe", "coffee_roastery", "coffee_stand"];

    private static readonly HashSet<string> FoodTypes =
    [
        "restaurant", "meal_takeaway", "meal_delivery", "food_court", "diner", "steak_house", "kebab_shop",
        "buffet", "bistro", "fine_dining_restaurant", "fast_food_restaurant", "deli", "sandwich_shop", "food"
    ];

    private static readonly HashSet<string> BarTypes =
    [
        "bar", "pub", "wine_bar", "cocktail_bar", "beer_garden", "brewpub", "sports_bar", "lounge_bar", "irish_pub",
        "hookah_bar", "gastropub", "bar_and_grill", "beer_hall", "brewery", "winery"
    ];

    private static readonly HashSet<string> NightlifeTypes = ["night_club", "dance_hall", "karaoke"];

    private static readonly HashSet<string> ShowTypes =
    [
        "movie_theater", "comedy_club", "live_music_venue", "concert_hall", "bowling_alley", "amusement_center",
        "video_arcade", "escape_room"
    ];

    private static readonly HashSet<string> CultureTypes =
    [
        "museum", "art_gallery", "performing_arts_theater", "opera_house", "amphitheatre", "cultural_center",
        "library", "art_museum", "history_museum", "philharmonic_hall", "planetarium", "auditorium", "cultural_landmark"
    ];

    private static readonly HashSet<string> OutdoorTypes =
    [
        "park", "city_park", "national_park", "state_park", "garden", "botanical_garden", "playground", "dog_park",
        "hiking_area", "beach", "plaza", "marina", "picnic_ground", "nature_reserve", "observation_deck",
        "scenic_spot", "tourist_attraction", "waterfront"
    ];

    /// <summary>Ana türün ait olduğu aile. Bilinmeyen tür <see cref="Family.Other"/> döner ve hiçbir niyete girmez.</summary>
    public static Family FamilyOf(string? primaryType)
    {
        var t = primaryType?.Trim().ToLowerInvariant() ?? "";
        if (t.Length == 0)
        {
            return Family.Other;
        }

        if (CafeTypes.Contains(t))
        {
            return Family.Cafe;
        }

        if (FoodTypes.Contains(t) || t.EndsWith("_restaurant", StringComparison.Ordinal))
        {
            return Family.Food;
        }

        if (BarTypes.Contains(t))
        {
            return Family.Bar;
        }

        if (NightlifeTypes.Contains(t))
        {
            return Family.Nightlife;
        }

        if (ShowTypes.Contains(t))
        {
            return Family.Show;
        }

        if (CultureTypes.Contains(t))
        {
            return Family.Culture;
        }

        return OutdoorTypes.Contains(t) ? Family.Outdoor : Family.Other;
    }

    private static IReadOnlyList<Family> Allowed(Intent intent) => intent switch
    {
        Intent.Kahve => [Family.Cafe],
        Intent.Yemek => [Family.Food],
        Intent.Eglence => [Family.Bar, Family.Nightlife, Family.Show],
        // Sinema hem Eğlence hem Kültür'de: OSM ve Google ikisinde de ayrı bir tür değil.
        Intent.Kultur => [Family.Culture],
        Intent.AcikHava => [Family.Outdoor],
        Intent.Gece => [Family.Bar, Family.Nightlife],
        _ => []
    };

    /// <summary>Mekan bu niyetin listesine girebilir mi? <paramref name="types"/>[0] ana türdür.</summary>
    public static bool Fits(Intent intent, string name, string? cuisine, IReadOnlyList<string> types)
    {
        if (types.Count == 0)
        {
            return false;
        }

        var primary = types[0];
        var family = FamilyOf(primary);
        var blob = $"{name} {cuisine}".ToLowerInvariant();
        var allowed = Allowed(intent).Contains(family)
                      || (intent == Intent.Kultur && primary == "movie_theater")
                      // "Swivel Cafe & Bakery" gibi kahve de sunan fırın/pastane Kahve'ye girer; düz fırın girmez.
                      || (intent == Intent.Kahve && primary is "bakery" or "dessert_shop" or "pastry_shop" && Has(blob, CoffeeWords));
        if (!allowed)
        {
            return false;
        }

        return intent switch
        {
            // OSM'de "cafe" diye etiketlenmiş suşi restoranı Kahve'de görünmesin.
            Intent.Kahve => !Has(blob, MealWords) || Has(blob, CoffeeWords),
            // Kültür merkezi/galeri diye etiketlenmiş bir restoran ya da kafe Kültür'de görünmesin.
            Intent.Kultur => !Has(blob, EateryWords) || Has(blob, CultureWords),
            // Park etiketli bir restoran ya da kafe Açık hava'da görünmesin.
            Intent.AcikHava => !Has(blob, EateryWords) || Has(blob, OutdoorWords),
            _ => true
        };
    }

    private static readonly string[] CoffeeWords =
        ["kahve", "cafe", "kafe", "coffee", "çay", "cay", "tea", "espresso"];

    private static readonly string[] MealWords =
        ["restoran", "restaurant", "sushi", "uzakdoğu", "uzakdogu"];

    private static readonly string[] EateryWords =
    [
        "restoran", "restaurant", "lokanta", "meyhane", "kebap", "kebab", "döner", "doner", "pide", "lahmacun",
        "burger", "pizza", "köfte", "kofte", "ocakbaşı", "ocakbasi", "bistro", "cafe", "kafe", "coffee", "kahve",
        "street food", "sofrası", "sofrasi", "mutfak"
    ];

    private static readonly string[] CultureWords =
    [
        "müze", "muze", "museum", "tiyatro", "theatre", "theater", "sahne", "sanat", "galeri", "gallery", "sinema",
        "cinema", "opera", "kütüphane", "kutuphane", "library", "kültür", "kultur", "konser", "salon"
    ];

    private static readonly string[] OutdoorWords =
        ["park", "bahçe", "bahce", "garden", "sahil", "koru", "meydan", "orman", "plaj", "beach"];

    private static bool Has(string blob, string[] words) => words.Any(w => blob.Contains(w, StringComparison.Ordinal));

    /// <summary>İki kayıt aynı türden mekan mı? Bir müzenin detayına içindeki kafenin yorumları gelmesin diye.</summary>
    public static bool Compatible(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var fa = a.Count > 0 ? FamilyOf(a[0]) : Family.Other;
        var fb = b.Count > 0 ? FamilyOf(b[0]) : Family.Other;
        if (fa == Family.Other || fb == Family.Other || fa == fb)
        {
            return true;
        }

        // Bar, gece kulübü ve sahne birbirine yakın; birinin diğeriyle eşleşmesi zararsız.
        static bool Evening(Family f) => f is Family.Bar or Family.Nightlife or Family.Show;
        return Evening(fa) && Evening(fb);
    }

    /// <summary>Kayıt yalnızca ana türünü bilen bir mekan için hangi niyete aittir? Bilinmiyorsa null.</summary>
    public static Intent? IntentOf(IReadOnlyList<string> types, string name = "", string? cuisine = null)
    {
        foreach (var intent in (Intent[])[Intent.Kahve, Intent.Yemek, Intent.Kultur, Intent.AcikHava, Intent.Gece, Intent.Eglence])
        {
            if (Fits(intent, name, cuisine, types))
            {
                return intent;
            }
        }

        return null;
    }

    public static string NominatimQuery(Intent intent) => intent switch
    {
        Intent.Kahve => "cafe",
        Intent.Yemek => "restaurant",
        Intent.Eglence => "bar",
        Intent.Kultur => "museum",
        Intent.AcikHava => "park",
        Intent.Gece => "bar",
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, null)
    };

    /// <summary>
    /// OSM etiketinden ana tür. Tanımadığımız etiket (eczane, otel, giyim mağazası…) "establishment" olur
    /// ve hiçbir niyete girmez. Önceden bunlar "tourist_attraction" sayılıp Açık hava'ya düşüyordu.
    /// </summary>
    public static string GoogleTypeFromOsm(string? amenity, string? shop, string? leisure, string? tourism)
    {
        if (amenity is "cafe" || shop is "coffee")
        {
            return "cafe";
        }

        if (amenity is "restaurant" or "food_court")
        {
            return "restaurant";
        }

        if (amenity is "fast_food")
        {
            return "fast_food_restaurant";
        }

        if (shop is "bakery")
        {
            return "bakery";
        }

        if (amenity is "ice_cream")
        {
            return "ice_cream_shop";
        }

        if (amenity is "nightclub")
        {
            return "night_club";
        }

        if (amenity is "bar" or "pub")
        {
            return "bar";
        }

        if (amenity is "cinema")
        {
            return "movie_theater";
        }

        if (amenity is "theatre")
        {
            return "performing_arts_theater";
        }

        if (amenity is "library")
        {
            return "library";
        }

        if (amenity is "arts_centre")
        {
            return "cultural_center";
        }

        if (tourism is "gallery")
        {
            return "art_gallery";
        }

        if (amenity is "museum" || tourism is "museum")
        {
            return "museum";
        }

        if (leisure is "park" or "garden" or "playground" or "nature_reserve" || amenity is "park")
        {
            return "park";
        }

        if (tourism is "attraction" or "viewpoint" || leisure is "beach_resort")
        {
            return "tourist_attraction";
        }

        return "establishment";
    }
}
