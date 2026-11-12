using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public sealed class PlaceAmenityChipsTests
{
    [Fact]
    public void Builds_wifi_outdoor_cuisine_phone_web()
    {
        var chips = PlaceAmenityChips.From("coffee_shop;turkish", "yes", "wlan", true, true);
        Assert.Contains("Kahve", chips);
        Assert.Contains("Türk", chips);
        Assert.Contains("Açık oturma", chips);
        Assert.Contains("Wi-Fi", chips);
        Assert.DoesNotContain("Telefon", chips);
        Assert.DoesNotContain("Web", chips);
    }

    [Fact]
    public void Skips_no_and_empty()
    {
        Assert.Empty(PlaceAmenityChips.From(null, "no", "no", false, false));
    }
}
