using Nereye.Domain;
using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class IntentPlaceTypesTests
{
    [Fact]
    public void Kahve_includes_cafe()
    {
        Assert.Contains("cafe", IntentPlaceTypes.GoogleTypes(Intent.Kahve));
        Assert.Contains("coffee_shop", IntentPlaceTypes.GoogleTypes(Intent.Kahve));
    }

    [Fact]
    public void Kultur_includes_theater_and_museum()
    {
        var types = IntentPlaceTypes.GoogleTypes(Intent.Kultur);
        Assert.Contains("performing_arts_theater", types);
        Assert.Contains("museum", types);
        Assert.Contains("movie_theater", types);
    }

    [Fact]
    public void Every_intent_has_at_least_one_type()
    {
        foreach (var intent in Enum.GetValues<Intent>())
        {
            Assert.NotEmpty(IntentPlaceTypes.GoogleTypes(intent));
            Assert.NotEmpty(IntentPlaceTypes.OsmTags(intent));
            Assert.False(string.IsNullOrWhiteSpace(IntentPlaceTypes.NominatimQuery(intent)));
        }
    }

    [Fact]
    public void Kahve_drops_a_sushi_restaurant_tagged_as_cafe()
    {
        Assert.False(IntentPlaceTypes.Fits(Intent.Kahve, "Sushi City Alsancak uzakdoğu restorani", "sushi", ["cafe"]));
        Assert.True(IntentPlaceTypes.Fits(Intent.Kahve, "Montag Coffee Roasters", "coffee_shop", ["cafe"]));
        Assert.True(IntentPlaceTypes.Fits(Intent.Kahve, "OMBOR", "tea", ["cafe"]));
    }

    [Fact]
    public void Osm_cafe_maps_to_google_cafe()
    {
        Assert.Equal("cafe", IntentPlaceTypes.GoogleTypeFromOsm("cafe", null, null, null));
        Assert.Equal("park", IntentPlaceTypes.GoogleTypeFromOsm(null, null, "park", null));
    }
}

/// <summary>
/// Örnekler Kadıköy için Google Places'ın gerçek yanıtlarından alındı (ad, ana tür, diğer türler).
/// Eski kural "herhangi bir tür uyarsa" diyordu; bunların hepsi yanlış listeye giriyordu.
/// </summary>
public class IntentFitsByPrimaryTypeTests
{
    private static bool Fits(Intent intent, string name, params string[] types) =>
        IntentPlaceTypes.Fits(intent, name, null, types);

    [Theory]
    [InlineData("Muutto Street Food Moda", new[] { "restaurant", "tourist_attraction", "spanish_restaurant", "bar" })]
    [InlineData("Şakirin Camii", new[] { "mosque", "tourist_attraction", "place_of_worship" })]
    [InlineData("KidZania", new[] { "amusement_center", "tourist_attraction", "amusement_park" })]
    public void Acik_hava_rejects_restaurants_mosques_and_indoor_venues(string name, string[] types) =>
        Assert.False(Fits(Intent.AcikHava, name, types));

    [Theory]
    [InlineData("Moda Sahil Parkı ve Yürüyüş Yolu", new[] { "park" })]
    [InlineData("Umbrella Street", new[] { "tourist_attraction" })]
    [InlineData("Koşuyolu Parkı", new[] { "park", "tourist_attraction" })]
    public void Acik_hava_keeps_parks_and_open_spots(string name, string[] types) =>
        Assert.True(Fits(Intent.AcikHava, name, types));

    [Theory]
    [InlineData("Sojubar İstanbul", new[] { "korean_restaurant", "bar", "restaurant", "food" })]
    [InlineData("Küp Saklı Bahçe Kadıköy", new[] { "restaurant", "beer_garden", "pub", "cafe", "bar" })]
    [InlineData("Moda Deniz Kulübü 1935", new[] { "association_or_organization", "hotel", "bar", "restaurant" })]
    public void Eglence_and_gece_reject_restaurants_and_clubs_that_merely_have_a_bar(string name, string[] types)
    {
        Assert.False(Fits(Intent.Eglence, name, types));
        Assert.False(Fits(Intent.Gece, name, types));
    }

    [Theory]
    [InlineData("Viktor Levi Şarap Evi", new[] { "wine_bar", "cocktail_bar", "bar", "restaurant" })]
    [InlineData("Ağaç Ev Kadıköy", new[] { "pub", "coffee_shop", "cafe", "gastropub" })]
    [InlineData("Karga", new[] { "bar", "concert_hall", "night_club" })]
    public void Gece_keeps_bars_whatever_their_secondary_types(string name, string[] types)
    {
        Assert.True(Fits(Intent.Gece, name, types));
        Assert.False(Fits(Intent.Kahve, name, types));
        Assert.False(Fits(Intent.Yemek, name, types));
    }

    [Theory]
    [InlineData("Dorock XL", new[] { "concert_hall", "bar" })]
    [InlineData("Aylak Kadıköy", new[] { "comedy_club", "event_venue", "bar" })]
    [InlineData("Kadıköy Sineması", new[] { "movie_theater" })]
    public void Eglence_keeps_shows_and_cinemas(string name, string[] types) =>
        Assert.True(Fits(Intent.Eglence, name, types));

    [Theory]
    [InlineData("Müze Gazhane", new[] { "museum" })]
    [InlineData("KADIKÖY BELEDİYESİ SÜREYYA OPERASI", new[] { "opera_house", "concert_hall", "performing_arts_theater" })]
    [InlineData("Özgürlük Parkı Amfi Tiyatro", new[] { "amphitheatre", "performing_arts_theater" })]
    [InlineData("Kadıköy Sineması", new[] { "movie_theater" })]
    [InlineData("Kahve Müzesi", new[] { "museum" })]
    public void Kultur_keeps_museums_stages_and_cinemas(string name, string[] types) =>
        Assert.True(Fits(Intent.Kultur, name, types));

    [Theory]
    [InlineData("Gazhane Cafe & Restaurant", new[] { "restaurant", "museum" })]
    [InlineData("Çiya Sofrası", new[] { "turkish_restaurant", "tourist_attraction" })]
    // OSM'de kültür merkezi diye etiketli ama adından restoran olduğu belli
    [InlineData("Moda Bistro Restaurant", new[] { "cultural_center" })]
    [InlineData("Galeri Kafe", new[] { "cafe", "art_gallery" })]
    public void Kultur_rejects_places_to_eat(string name, string[] types) =>
        Assert.False(Fits(Intent.Kultur, name, types));

    [Fact]
    public void Kahve_takes_a_bakery_only_when_it_is_also_a_cafe()
    {
        Assert.True(Fits(Intent.Kahve, "Swivel Cafe & Bakery", "bakery", "cafe"));
        Assert.False(Fits(Intent.Kahve, "Mahalle Fırını", "bakery"));
        Assert.False(Fits(Intent.Kahve, "Zeytin Kafe & Bar", "bar", "cafe", "food"));
    }

    [Fact]
    public void Unknown_or_empty_primary_type_fits_nothing()
    {
        foreach (var intent in Enum.GetValues<Intent>())
        {
            Assert.False(Fits(intent, "Eczane", "establishment"));
            Assert.False(Fits(intent, "Boş"));
        }
    }

    [Theory]
    [InlineData("pharmacy", null, null, null)]
    [InlineData(null, "clothes", null, null)]
    [InlineData(null, null, null, "hotel")]
    public void Unknown_osm_tags_no_longer_count_as_tourist_attractions(string? amenity, string? shop, string? leisure, string? tourism)
    {
        var type = IntentPlaceTypes.GoogleTypeFromOsm(amenity, shop, leisure, tourism);
        Assert.Equal("establishment", type);
        Assert.False(Fits(Intent.AcikHava, "X", type));
    }

    [Fact]
    public void Osm_tags_map_to_the_right_list()
    {
        Assert.True(Fits(Intent.Kultur, "Kadıköy Kütüphanesi", IntentPlaceTypes.GoogleTypeFromOsm("library", null, null, null)));
        Assert.True(Fits(Intent.Kultur, "Mixer", IntentPlaceTypes.GoogleTypeFromOsm(null, null, null, "gallery")));
        Assert.True(Fits(Intent.Eglence, "Rexx", IntentPlaceTypes.GoogleTypeFromOsm("cinema", null, null, null)));
        Assert.True(Fits(Intent.Yemek, "Burger Lab", IntentPlaceTypes.GoogleTypeFromOsm("fast_food", null, null, null)));
        Assert.False(Fits(Intent.Kahve, "Dondurmacı", IntentPlaceTypes.GoogleTypeFromOsm("ice_cream", null, null, null)));
    }

    [Fact]
    public void A_museum_is_not_merged_with_the_restaurant_inside_it()
    {
        var museum = new PlaceSnapshot("osm:way:1", "Müze Gazhane", null, 40.995, 29.03, ["museum"], null, null, null, null, null, [], "");
        var restaurant = museum with { PlaceId = "g:x", Name = "Müze Gazhane Cafe & Restaurant", Types = ["restaurant", "cafe"] };
        var stage = museum with { PlaceId = "g:y", Name = "Müze Gazhane Sahnesi", Types = ["performing_arts_theater"] };
        Assert.False(PlaceMerge.Same(museum, restaurant));
        Assert.Null(PlaceMerge.Pick(museum, [restaurant]));
        Assert.True(PlaceMerge.Same(museum, stage));
    }

    [Fact]
    public void Inferred_intent_uses_the_primary_type()
    {
        Assert.Equal(Intent.Yemek, IntentPlaceTypes.IntentOf(["korean_restaurant", "bar"]));
        Assert.Equal(Intent.Kultur, IntentPlaceTypes.IntentOf(["museum", "cafe"]));
        Assert.Null(IntentPlaceTypes.IntentOf(["mosque", "tourist_attraction"]));
    }
}
