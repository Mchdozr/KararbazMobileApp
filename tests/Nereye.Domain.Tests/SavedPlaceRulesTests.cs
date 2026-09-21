using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class SavedPlaceRulesTests
{
    [Fact]
    public void Caps_at_twenty()
    {
        Assert.True(SavedPlaceRules.CanAdd(19));
        Assert.False(SavedPlaceRules.CanAdd(20));
    }

    [Fact]
    public void Maps_turkish_kinds()
    {
        Assert.Equal("home", SavedPlaceRules.NormalizeKind("Ev"));
        Assert.Equal("work", SavedPlaceRules.NormalizeKind("iş"));
        Assert.Equal("custom", SavedPlaceRules.NormalizeKind("Nişantaşı"));
    }

    [Fact]
    public void Rejects_null_island()
    {
        Assert.False(SavedPlaceRules.ValidCoord(0, 0));
        Assert.True(SavedPlaceRules.ValidCoord(41.055, 28.987));
    }
}

public class AvatarFileTests
{
    [Fact]
    public void Accepts_jpeg_magic()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }.Concat(new byte[32]).ToArray();
        Assert.True(AvatarFile.TryDetect(bytes, out var ext));
        Assert.Equal(".jpg", ext);
    }

    [Fact]
    public void Rejects_empty_and_huge()
    {
        Assert.False(AvatarFile.TryDetect([], out _));
        Assert.False(AvatarFile.TryDetect(new byte[2_000_001], out _));
    }

    [Fact]
    public void Decodes_data_url()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }.Concat(new byte[32]).ToArray();
        var payload = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
        Assert.True(AvatarFile.TryDecodeBase64(payload, out var bytes));
        Assert.True(AvatarFile.TryDetect(bytes, out var ext));
        Assert.Equal(".jpg", ext);
    }
}

public class GeoSuggestMapperTests
{
    [Fact]
    public void Parses_suburb_hit()
    {
        const string json = """
            [
              {
                "lat": "41.055",
                "lon": "28.987",
                "class": "place",
                "type": "suburb",
                "name": "Halide Edip Adıvar",
                "display_name": "Halide Edip Adıvar, Şişli, İstanbul"
              }
            ]
            """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var hits = GeoSuggestMapper.Parse(doc.RootElement);
        Assert.Single(hits);
        Assert.Equal("Halide Edip Adıvar", hits[0].Label);
        Assert.Equal(41.055, hits[0].Latitude, 3);
        Assert.Equal(28.987, hits[0].Longitude, 3);
    }
}
