namespace Nereye.Domain.Places;

public static class IntentPlaceTypes
{
    public static IReadOnlyList<string> GoogleTypes(Intent intent) => intent switch
    {
        Intent.Kahve => ["cafe", "coffee_shop"],
        Intent.Yemek => ["restaurant", "meal_takeaway"],
        Intent.Eglence => ["bar", "night_club", "bowling_alley"],
        Intent.Kultur => ["performing_arts_theater", "movie_theater", "museum"],
        Intent.AcikHava => ["park", "tourist_attraction"],
        Intent.Gece => ["night_club", "bar"],
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, null)
    };

    public static IReadOnlyList<(string Key, string Value)> OsmTags(Intent intent) => intent switch
    {
        Intent.Kahve => [("amenity", "cafe"), ("shop", "coffee")],
        Intent.Yemek => [("amenity", "restaurant"), ("amenity", "fast_food")],
        Intent.Eglence => [("amenity", "bar"), ("amenity", "pub"), ("amenity", "cinema")],
        Intent.Kultur => [("amenity", "theatre"), ("amenity", "museum"), ("tourism", "museum"), ("amenity", "arts_centre"), ("amenity", "library")],
        Intent.AcikHava => [("leisure", "park"), ("leisure", "garden"), ("amenity", "park")],
        Intent.Gece => [("amenity", "bar"), ("amenity", "pub"), ("amenity", "nightclub")],
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, null)
    };

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

    public static string GoogleTypeFromOsm(string? amenity, string? shop, string? leisure, string? tourism)
    {
        if (amenity is "cafe" || shop is "coffee")
        {
            return "cafe";
        }

        if (amenity is "restaurant" or "fast_food" or "food_court")
        {
            return "restaurant";
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

        if (amenity is "museum" or "arts_centre" or "library" || tourism is "museum" or "gallery")
        {
            return "museum";
        }

        if (leisure is "park" or "garden" || amenity is "park")
        {
            return "park";
        }

        return "tourist_attraction";
    }
}
