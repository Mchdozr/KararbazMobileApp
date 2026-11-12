using System.Text.Json;
using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public sealed class GooglePlaceJsonTests
{
    private const string Sample = """
    {
      "id": "ChIJabc",
      "displayName": { "text": "Brekkie Breakfast Club", "languageCode": "tr" },
      "formattedAddress": "Caferağa, Moda Cd. No:1, 34710 Kadıköy/İstanbul, Türkiye",
      "shortFormattedAddress": "Caferağa, Moda Cd. No:1, Kadıköy",
      "location": { "latitude": 40.9871, "longitude": 29.0262 },
      "types": ["breakfast_restaurant", "cafe"],
      "primaryTypeDisplayName": { "text": "kahvaltı restoranı" },
      "rating": 4.3,
      "userRatingCount": 4898,
      "priceLevel": "PRICE_LEVEL_MODERATE",
      "nationalPhoneNumber": "(0216) 123 45 67",
      "websiteUri": "https://brekkie.example",
      "googleMapsUri": "https://maps.google.com/?cid=1",
      "currentOpeningHours": { "openNow": false },
      "regularOpeningHours": { "openNow": true, "weekdayDescriptions": ["Pazartesi: 08:00–22:00", "Salı: Kapalı"] },
      "editorialSummary": { "text": "Kahvaltı ve brunch sunan bohem mekan." },
      "photos": [
        { "name": "places/ChIJabc/photos/one" }, { "name": "places/ChIJabc/photos/two" },
        { "name": "https://evil.example/x.jpg" }
      ],
      "outdoorSeating": true, "servesBreakfast": true, "delivery": false,
      "servesWine": true, "servesBeer": true,
      "accessibilityOptions": { "wheelchairAccessibleEntrance": true },
      "paymentOptions": { "acceptsCreditCards": true },
      "parkingOptions": { "freeStreetParking": true, "paidParkingLot": false },
      "reviews": [
        { "rating": 5, "text": { "text": "  Harika kahvaltı.  " }, "relativePublishTimeDescription": "2 hafta önce",
          "authorAttribution": { "displayName": "Elif", "photoUri": "https://lh3.googleusercontent.com/a/x" } },
        { "rating": 4, "authorAttribution": { "displayName": "Yorumsuz" } },
        { "rating": 3.6, "text": { "text": "İdare eder." }, "authorAttribution": { "photoUri": "javascript:alert(1)" } }
      ]
    }
    """;

    private static PlaceSnapshot Parsed() => GooglePlaceJson.Parse(JsonDocument.Parse(Sample).RootElement)!;

    [Fact]
    public void Core_fields_are_read()
    {
        var p = Parsed();
        Assert.Equal("ChIJabc", p.PlaceId);
        Assert.Equal("Brekkie Breakfast Club", p.Name);
        Assert.Equal("Caferağa, Moda Cd. No:1, Kadıköy", p.Address);
        Assert.Equal(4.3, p.GoogleRating);
        Assert.Equal(4898, p.GoogleUserRatingCount);
        Assert.Equal(2, p.PriceLevel);
        Assert.Equal("Kahvaltı restoranı", p.Category);
        Assert.Equal("Kahvaltı ve brunch sunan bohem mekan.", p.Summary);
        Assert.Equal("Pazartesi: 08:00–22:00; Salı: Kapalı", p.OpeningHoursText);
    }

    [Fact]
    public void Current_hours_win_over_regular_hours() => Assert.False(Parsed().OpenNow);

    [Fact]
    public void Only_google_photo_names_are_kept()
    {
        var p = Parsed();
        Assert.Equal(["places/ChIJabc/photos/one", "places/ChIJabc/photos/two"], p.Photos);
        Assert.Equal("places/ChIJabc/photos/one", p.PhotoReference);
    }

    [Fact]
    public void Features_are_turkish_labels_for_true_flags_only()
    {
        var f = Parsed().Features!;
        Assert.Contains("Açık alan", f);
        Assert.Contains("Kahvaltı", f);
        Assert.DoesNotContain("Eve teslimat", f);
        Assert.Single(f, x => x == "Alkollü içecek");
        Assert.Contains("Tekerlekli sandalyeye uygun", f);
        Assert.Contains("Kart geçerli", f);
        Assert.Contains("Otopark", f);
    }

    [Fact]
    public void Reviews_without_text_are_dropped_and_untrusted_avatar_is_removed()
    {
        var r = Parsed().Reviews!;
        Assert.Equal(2, r.Count);
        Assert.Equal(new PlaceReview("Elif", 5, "Harika kahvaltı.", "2 hafta önce", "https://lh3.googleusercontent.com/a/x"), r[0]);
        Assert.Equal("Google kullanıcısı", r[1].Author);
        Assert.Equal(4, r[1].Rating);
        Assert.Null(r[1].AuthorPhotoUrl);
    }

    [Fact]
    public void Sparse_or_broken_payloads_do_not_throw()
    {
        Assert.Null(GooglePlaceJson.Parse(JsonDocument.Parse("{}").RootElement));
        Assert.Empty(GooglePlaceJson.ParseList(JsonDocument.Parse("{}").RootElement));
        var bare = GooglePlaceJson.Parse(JsonDocument.Parse("""{"id":"x","rating":"n/a","photos":{}}""").RootElement)!;
        Assert.Equal("x", bare.Name);
        Assert.Null(bare.GoogleRating);
        Assert.Null(bare.Photos);
        Assert.Null(bare.Features);
        Assert.Null(bare.Reviews);
    }

    [Fact]
    public void Overlay_keeps_osm_identity_and_fills_gaps_from_google()
    {
        var osm = new PlaceSnapshot("osm:node:1", "Brekkie", null, 40.9871, 29.0262, ["cafe"], null, null, null, null, null, [],
            "Harita verisi: OpenStreetMap", Phone: "0216 000 00 00");
        var merged = PlaceMerge.Overlay(osm, Parsed());
        Assert.Equal("osm:node:1", merged.PlaceId);
        Assert.Equal("Brekkie", merged.Name);
        Assert.Equal("0216 000 00 00", merged.Phone);
        Assert.Equal("https://brekkie.example", merged.Website);
        Assert.Equal(4.3, merged.GoogleRating);
        Assert.NotNull(merged.Reviews);
        Assert.Contains("Google", merged.Attribution);
    }

    [Fact]
    public void Pick_refuses_a_different_venue_next_door()
    {
        var target = new PlaceSnapshot("osm:node:1", "Petra Roasting", null, 40.9871, 29.0262, [], null, null, null, null, null, [], "");
        var google = Parsed();
        Assert.Null(PlaceMerge.Pick(target, [google]));
        Assert.NotNull(PlaceMerge.Pick(target with { Name = "Brekkie Breakfast Club Moda" }, [google]));
        Assert.Null(PlaceMerge.Pick(target with { Name = "Brekkie Breakfast Club", Latitude = 41.05 }, [google]));
    }
}
