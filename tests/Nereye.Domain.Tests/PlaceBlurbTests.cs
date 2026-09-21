using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public sealed class PlaceBlurbTests
{
    [Fact]
    public void Petra_is_warm_and_specific()
    {
        var note = PlaceBlurb.Compose(Snap("nereye:petra", "Petra Roasting Co.", "Caferağa, Kadıköy", ["cafe"]), 180);
        Assert.Contains("çekirdek", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kadıköy", note, StringComparison.OrdinalIgnoreCase);
        Assert.True(note.Length >= 40);
    }

    [Fact]
    public void Generic_cafe_mentions_name_and_walk()
    {
        var note = PlaceBlurb.Compose(
            Snap("osm:node:1", "Deniz Kahve", "Alsancak, İzmir", ["cafe", "coffee_shop"]),
            220);
        Assert.Contains("Deniz Kahve", note);
        Assert.True(note.Contains("yürüyüş", StringComparison.OrdinalIgnoreCase)
                    || note.Contains("Alsancak", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Same_id_is_stable()
    {
        var place = Snap("osm:node:99", "Mavi Fincan", "Karşıyaka", ["cafe"]);
        Assert.Equal(PlaceBlurb.Compose(place, 300), PlaceBlurb.Compose(place, 300));
    }

    private static PlaceSnapshot Snap(string id, string name, string address, string[] types) =>
        new(id, name, address, 38.4, 27.1, types, true, null, null, null, null, [], "Harita verisi: OpenStreetMap");
}
