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
