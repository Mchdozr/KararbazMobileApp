using System.Globalization;
using System.Text.Json;
using Nereye.Domain.Geo;

namespace Nereye.Domain.Places;

public static class NominatimPlaceMapper
{
    private const string Attribution = "Harita verisi: OpenStreetMap";

    public static IReadOnlyList<PlaceSnapshot> ParseArray(
        JsonElement json,
        double originLat,
        double originLng,
        int radiusMeters)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var radius = Math.Clamp(radiusMeters, 50, 8000);
        var items = new List<PlaceSnapshot>();
        foreach (var el in json.EnumerateArray())
        {
            var parsed = ParseElement(el);
            if (parsed is null)
            {
                continue;
            }

            if (originLat != 0 || originLng != 0)
            {
                var d = GeoMath.HaversineMeters(originLat, originLng, parsed.Latitude, parsed.Longitude);
                if (d > radius)
                {
                    continue;
                }
            }

            items.Add(parsed);
        }

        return items
            .OrderBy(p => GeoMath.HaversineMeters(originLat, originLng, p.Latitude, p.Longitude))
            .ToList();
    }

    public static PlaceSnapshot? ParseElement(JsonElement el)
    {
        var cls = Str(el, "class") ?? Str(el, "category");
        if (cls is not ("amenity" or "shop" or "leisure" or "tourism"))
        {
            return null;
        }

        var osmType = NormalizeType(Str(el, "osm_type"));
        var osmId = OsmId(el);
        if (string.IsNullOrWhiteSpace(osmId))
        {
            return null;
        }

        if (!TryCoord(el, out var lat, out var lng))
        {
            return null;
        }

        var name = Str(el, "name") ?? AddressField(el, "amenity") ?? AddressField(el, "shop");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var type = Str(el, "type");
        var amenity = cls == "amenity" ? type : null;
        var shop = cls == "shop" ? type : null;
        var leisure = cls == "leisure" ? type : null;
        var tourism = cls == "tourism" ? type : null;
        var googleType = IntentPlaceTypes.GoogleTypeFromOsm(amenity, shop, leisure, tourism);
        var address = AddressField(el, "road")
                      ?? AddressField(el, "suburb")
                      ?? AddressField(el, "neighbourhood")
                      ?? AfterComma(Str(el, "display_name"));
        var maps = $"https://www.openstreetmap.org/{osmType}/{osmId}";
        var photo = PlacePhoto.FromNominatim(el);
        var hoursText = PlacePhoto.ExtraFromNominatim(el, "opening_hours");
        var hours = OsmOpeningHours.Parse(hoursText);
        var open = OsmOpeningHours.IsOpen(hoursText, DateTimeOffset.Now);
        var phone = PlacePhoto.ExtraFromNominatim(el, "phone", "contact:phone");
        return new PlaceSnapshot(
            $"osm:{osmType}:{osmId}",
            name,
            address,
            lat,
            lng,
            [googleType],
            open,
            photo,
            null,
            null,
            maps,
            hours,
            Attribution,
            PlacePhoto.WebsiteFromNominatim(el),
            PlacePhoto.WikidataFromNominatim(el),
            phone,
            hoursText);
    }

    private static string? AddressField(JsonElement el, string key)
    {
        if (!el.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return Str(address, key);
    }

    private static string? AfterComma(string? display)
    {
        if (string.IsNullOrWhiteSpace(display))
        {
            return null;
        }

        var i = display.IndexOf(',');
        return i < 0 ? display : display[(i + 1)..].Trim();
    }

    private static bool TryCoord(JsonElement el, out double lat, out double lng)
    {
        lat = Num(el, "lat");
        lng = Num(el, "lon");
        return lat != 0 || lng != 0;
    }

    private static double Num(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v))
        {
            return 0;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String => double.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var n) ? n : 0,
            _ => 0
        };
    }

    private static string? OsmId(JsonElement el)
    {
        if (!el.TryGetProperty("osm_id", out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetInt64().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => v.GetString(),
            _ => null
        };
    }

    private static string NormalizeType(string? osmType) => osmType?.Trim().ToLowerInvariant() switch
    {
        "n" or "node" => "node",
        "w" or "way" => "way",
        "r" or "relation" => "relation",
        _ => "node"
    };

    private static string? Str(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
