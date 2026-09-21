using System.Globalization;
using System.Text.Json;

namespace Nereye.Domain.Places;

public sealed record GeoHit(string Label, double Latitude, double Longitude, string Kind);

public static class GeoSuggestMapper
{
    public static IReadOnlyList<GeoHit> Parse(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var hits = new List<GeoHit>();
        foreach (var el in json.EnumerateArray())
        {
            var lat = Num(el, "lat");
            var lng = Num(el, "lon");
            if (!SavedPlaceRules.ValidCoord(lat, lng))
            {
                continue;
            }

            var name = Str(el, "name") ?? FirstComma(Str(el, "display_name"));
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var kind = Str(el, "type") ?? Str(el, "class") ?? "place";
            hits.Add(new GeoHit(name.Trim(), lat, lng, kind));
            if (hits.Count >= 12)
            {
                break;
            }
        }

        return hits;
    }

    private static string? FirstComma(string? display)
    {
        if (string.IsNullOrWhiteSpace(display))
        {
            return null;
        }

        var i = display.IndexOf(',');
        return i < 0 ? display : display[..i].Trim();
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

    private static string? Str(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
