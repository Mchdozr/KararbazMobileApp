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
                "display_name": "Halide Edip Adıvar, Şişli, İstanbul",
                "address": { "suburb": "Halide Edip Adıvar", "town": "Şişli", "city": "İstanbul" }
              }
            ]
            """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var hits = GeoSuggestMapper.Parse(doc.RootElement);
        Assert.Single(hits);
        Assert.Equal("Halide Edip Adıvar", hits[0].Label);
        Assert.Equal("Şişli · İstanbul", hits[0].Subtitle);
        Assert.Equal("Mahalle", hits[0].KindLabel);
        Assert.Equal("Halide Edip Adıvar, Şişli", hits[0].ChipLabel);
        Assert.Equal(41.055, hits[0].Latitude, 3);
        Assert.Equal(28.987, hits[0].Longitude, 3);
    }

    [Fact]
    public void Esenler_hits_get_unique_subtitles_and_dedupe()
    {
        const string json = """
            [
              {
                "lat": "41.0431", "lon": "28.8762", "osm_id": 223474,
                "class": "boundary", "type": "administrative", "addresstype": "city_district",
                "name": "Esenler", "display_name": "Esenler, İstanbul, Türkiye",
                "address": { "city_district": "Esenler", "city": "İstanbul", "country": "Türkiye" }
              },
              {
                "lat": "41.0504", "lon": "28.8901", "osm_id": 771001,
                "class": "place", "type": "suburb",
                "name": "Esenler", "display_name": "Esenler Mahallesi, Güngören, İstanbul",
                "address": { "suburb": "Esenler", "town": "Güngören", "city": "İstanbul" }
              },
              {
                "lat": "41.0410", "lon": "28.8720", "osm_id": 881002,
                "class": "place", "type": "neighbourhood",
                "name": "Tuna", "display_name": "Tuna, Esenler, İstanbul",
                "address": { "neighbourhood": "Tuna", "city_district": "Esenler", "city": "İstanbul" }
              },
              {
                "lat": "41.0448", "lon": "28.8880", "osm_id": 991003,
                "class": "railway", "type": "station",
                "name": "Esenler", "display_name": "Esenler, Esenler, İstanbul",
                "address": { "city_district": "Esenler", "city": "İstanbul" }
              },
              {
                "lat": "41.0431", "lon": "28.8762", "osm_id": 223474,
                "class": "boundary", "type": "administrative", "name": "Esenler",
                "display_name": "Esenler kopya"
              },
              {
                "lat": "41.04312", "lon": "28.87621", "osm_id": 223475,
                "class": "boundary", "type": "administrative", "addresstype": "city_district",
                "name": "Esenler", "display_name": "Esenler yakın kopya",
                "address": { "city_district": "Esenler", "city": "İstanbul" }
              }
            ]
            """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var hits = GeoSuggestMapper.Parse(doc.RootElement);
        Assert.Equal(4, hits.Count);
        Assert.Equal(["Tuna", "Esenler", "Esenler", "Esenler"], hits.Select(h => h.Label).ToArray());
        Assert.Equal("Esenler · İstanbul", hits[0].Subtitle);
        Assert.Equal("Tuna, Esenler", hits[0].ChipLabel);
        Assert.Contains(hits, h => h.KindLabel == "Mahalle" && h.Subtitle.Contains("Güngören", StringComparison.Ordinal));
        Assert.Contains(hits, h => h.KindLabel == "İlçe" && h.Subtitle == "İstanbul");
        Assert.Contains(hits, h => h.KindLabel == "Durak");
        Assert.Equal(hits.Count, hits.Select(h => h.Label + "|" + h.Subtitle + "|" + h.Kind).Distinct().Count());
    }
}
