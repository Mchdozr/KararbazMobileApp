using System.Globalization;
using System.Text.Json;
using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class NominatimPlaceMapperTests
{
    private const string SisliCafeJson = """
        [
          {
            "osm_type": "node",
            "osm_id": 9251110317,
            "lat": "41.0559177",
            "lon": "28.9814455",
            "class": "amenity",
            "type": "cafe",
            "name": "MOC - Ministry of Coffee",
            "display_name": "MOC - Ministry of Coffee, Feriköy Fırın Sokağı, Şişli, İstanbul",
            "address": { "road": "Feriköy Fırın Sokağı", "suburb": "Cumhuriyet Mahallesi" },
            "extratags": {
              "wikimedia_commons": "File:Osmanbey in July 2024.jpg",
              "website": "https://moc.example/sisli",
              "wikidata": "Q42"
            }
          },
          {
            "osm_type": "way",
            "osm_id": 1,
            "lat": "40.0",
            "lon": "29.0",
            "class": "highway",
            "type": "residential",
            "name": "Cafe",
            "display_name": "Cafe, India"
          }
        ]
        """;

    [Fact]
    public void Viewbox_contains_origin()
    {
        var box = NominatimViewBox.Format(41.055, 28.987, 1500);
        var parts = box.Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(4, parts.Length);
        var left = parts[0];
        var top = parts[1];
        var right = parts[2];
        var bottom = parts[3];
        Assert.True(left < 28.987 && 28.987 < right);
        Assert.True(bottom < 41.055 && 41.055 < top);
    }

    [Fact]
    public void Kahve_query_is_cafe()
    {
        Assert.Equal("cafe", IntentPlaceTypes.NominatimQuery(Intent.Kahve));
    }

    [Fact]
    public void Parse_keeps_nearby_cafe_and_osm_id()
    {
        using var doc = JsonDocument.Parse(SisliCafeJson);
        var items = NominatimPlaceMapper.ParseArray(doc.RootElement, 41.055, 28.987, 1500);
        Assert.Single(items);
        Assert.Equal("osm:node:9251110317", items[0].PlaceId);
        Assert.Equal("MOC - Ministry of Coffee", items[0].Name);
        Assert.Equal("cafe", items[0].Types[0]);
        Assert.Equal("Feriköy Fırın Sokağı", items[0].Address);
        Assert.Contains("OpenStreetMap", items[0].Attribution);
        Assert.Contains("Special:FilePath", items[0].PhotoReference);
        Assert.Contains("Osmanbey", items[0].PhotoReference);
        Assert.Equal("https://moc.example/sisli", items[0].Website);
        Assert.Equal("Q42", items[0].Wikidata);
    }

    [Fact]
    public void Title_must_match_venue_not_street()
    {
        Assert.True(PlacePhoto.TitleFitsVenue("Hafiz Mustafa 1864", "File:Hafiz_Mustafa_Osmanbey.jpg"));
        Assert.False(PlacePhoto.TitleFitsVenue("Hafiz Mustafa 1864", "File:Abdi İpekçi Caddesi.jpg"));
        Assert.False(PlacePhoto.TitleFitsVenue("Park Sanat", "File:Macka_Sanat_park_book_market.jpg"));
        Assert.True(PlacePhoto.LooksLikePersonFile("File:Yalda_Abbasî.jpg"));
        Assert.False(PlacePhoto.LooksLikePersonFile("File:Abdi İpekçi Caddesi.jpg"));
        Assert.False(PlacePhoto.LooksLikePersonFile("File:Osmanbey in July 2024.jpg"));
    }

    [Fact]
    public void Og_image_resolves_relative_url()
    {
        var html = """<meta property="og:image" content="/img/store.jpg">""";
        Assert.Equal("https://cafe.example/img/store.jpg", PlacePhoto.TryOgImage(html, "https://cafe.example/sube"));
    }

    [Fact]
    public void Wikidata_p18_becomes_commons_url()
    {
        using var doc = JsonDocument.Parse("""{"claims":{"P18":[{"mainsnak":{"datavalue":{"value":"Cafe.jpg"}}}]}}""");
        Assert.Contains("Cafe.jpg", PlacePhoto.FromWikidataClaims(doc.RootElement));
    }

    [Fact]
    public void Commons_url_skips_pdf()
    {
        Assert.Null(PlacePhoto.CommonsFileUrl("File:notes.pdf"));
        Assert.Contains("Special:FilePath", PlacePhoto.CommonsFileUrl("File:Cafe.jpg"));
    }

    [Fact]
    public void Parse_drops_places_outside_radius()
    {
        using var doc = JsonDocument.Parse(SisliCafeJson);
        var items = NominatimPlaceMapper.ParseArray(doc.RootElement, 41.055, 28.987, 50);
        Assert.Empty(items);
    }
}
