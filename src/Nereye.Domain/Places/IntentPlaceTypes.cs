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
        Intent.Kahve => [("amenity", "cafe"), ("shop", "coffee"), ("shop", "bakery"), ("amenity", "ice_cream")],
        Intent.Yemek => [("amenity", "restaurant"), ("amenity", "fast_food"), ("amenity", "food_court")],
        Intent.Eglence => [("amenity", "bar"), ("amenity", "pub"), ("amenity", "cinema"), ("amenity", "nightclub")],
        Intent.Kultur => [("amenity", "theatre"), ("amenity", "museum"), ("tourism", "museum"), ("tourism", "gallery"), ("amenity", "arts_centre"), ("amenity", "library")],
        Intent.AcikHava => [("leisure", "park"), ("leisure", "garden"), ("leisure", "playground"), ("leisure", "nature_reserve"), ("amenity", "park")],
        Intent.Gece => [("amenity", "bar"), ("amenity", "pub"), ("amenity", "nightclub")],
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, null)
    };

    public static bool Fits(Intent intent, string name, string? cuisine, IReadOnlyList<string> types)
    {
        var allowed = GoogleTypes(intent);
        if (!types.Any(t => allowed.Contains(t)))
        {
            return false;
        }

        if (intent != Intent.Kahve)
        {
            return true;
        }

        var blob = $"{name} {cuisine}".ToLowerInvariant();
        var coffee = blob.Contains("kahve") || blob.Contains("cafe") || blob.Contains("kafe")
                     || blob.Contains("coffee") || blob.Contains("çay") || blob.Contains("cay")
                     || blob.Contains("tea") || blob.Contains("espresso");
        var otherMeal = blob.Contains("restoran") || blob.Contains("restaurant")
                        || blob.Contains("sushi") || blob.Contains("uzakdoğu") || blob.Contains("uzakdogu");
        return !otherMeal || coffee;
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

    public static string GoogleTypeFromOsm(string? amenity, string? shop, string? leisure, string? tourism)
    {
        if (amenity is "cafe" or "ice_cream" || shop is "coffee" or "bakery")
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

        if (leisure is "park" or "garden" or "playground" or "nature_reserve" || amenity is "park")
        {
            return "park";
        }

        return "tourist_attraction";
    }
}
