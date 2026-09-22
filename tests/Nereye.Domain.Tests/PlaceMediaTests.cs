using System.Collections.Concurrent;
using System.Text.Json;
using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public sealed class PlaceMediaTests
{
    [Fact]
    public void Overlay_wins()
    {
        Assert.Equal("/media/venues/petra.jpg", PlaceMedia.Url("/media/venues/petra.jpg", "places/x/photos/y"));
    }

    [Fact]
    public void Google_photo_name_becomes_proxy()
    {
        var url = PlaceMedia.Url(null, "places/ChIJ/photos/ABC");
        Assert.Equal("/api/media/google?name=" + Uri.EscapeDataString("places/ChIJ/photos/ABC"), url);
    }

    [Fact]
    public void Local_media_passthrough()
    {
        Assert.Equal("/media/venues/petra.jpg", PlaceMedia.Url(null, "/media/venues/petra.jpg"));
    }
}

public sealed class PlacePhotoPickTests
{
    [Fact]
    public void Commons_nearby_requires_title_fit()
    {
        var rows = new PlacePhoto.CommonsGeoHit[]
        {
            new("File:Random_street_Osmanbey.jpg", 12),
            new("File:Portrait_Ali.jpg", 8),
            new("File:Petra_Roasting_Co_Kadikoy.jpg", 40)
        };
        Assert.Equal(
            PlacePhoto.CommonsFileUrl("File:Petra_Roasting_Co_Kadikoy.jpg"),
            PlacePhoto.PickCommonsNearby(rows, "Petra Roasting Co"));
        Assert.Null(PlacePhoto.PickCommonsNearby(rows[..2], "Kısa Cafe"));
    }

    [Fact]
    public void First_unused_url_is_unique_per_batch()
    {
        var claimed = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        Assert.Equal("https://a.jpg", PlacePhoto.FirstUnused(["https://a.jpg", "https://b.jpg"], claimed));
        Assert.Equal("https://b.jpg", PlacePhoto.FirstUnused(["https://a.jpg", "https://b.jpg"], claimed));
        Assert.Null(PlacePhoto.FirstUnused(["https://a.jpg", "https://b.jpg"], claimed));
    }

    [Fact]
    public void Mapillary_reads_thumb()
    {
        using var doc = JsonDocument.Parse("""{"data":[{"id":"1","thumb_1024_url":"https://images.mapillary.com/x/thumb"}]}""");
        Assert.Equal("https://images.mapillary.com/x/thumb", MapillaryPhoto.Parse(doc.RootElement));
        using var empty = JsonDocument.Parse("""{"data":[]}""");
        Assert.Null(MapillaryPhoto.Parse(empty.RootElement));
    }

    [Fact]
    public void Trusted_photo_rejects_selfie_and_keeps_google_or_commons()
    {
        Assert.True(PlacePhoto.LooksLikeSelfieOrPortrait("File:Cafe_selfie.jpg"));
        Assert.True(PlacePhoto.LooksLikeSelfieOrPortrait("https://upload.wikimedia.org/portrait-ali.jpg"));
        Assert.Equal(
            "places/ChIJ/photos/ABC",
            PlacePhoto.FirstTrusted(["https://x/selfie.jpg", "places/ChIJ/photos/ABC"]));
        Assert.Contains(
            "Special:FilePath",
            PlacePhoto.FirstTrusted(["File:Petra_Roasting_Co.jpg"]));
        Assert.Null(PlacePhoto.FirstTrusted(["File:Random_portrait.jpg", "https://img/selfie.png"]));
    }
}
