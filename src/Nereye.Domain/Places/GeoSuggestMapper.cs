using System.Globalization;
using System.Text.Json;

namespace Nereye.Domain.Places;

public sealed record GeoHit(
    string Label,
    double Latitude,
    double Longitude,
    string Kind,
    string Subtitle = "",
    string KindLabel = "",
    string ChipLabel = "");

public static class GeoSuggestMapper
{
    public static IReadOnlyList<GeoHit> Parse(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var seenOsm = new HashSet<string>(StringComparer.Ordinal);
        var seenCoord = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<(GeoHit Hit, int Rank)>();

        foreach (var el in json.EnumerateArray())
        {
            var lat = Num(el, "lat");
            var lng = Num(el, "lon");
            if (!SavedPlaceRules.ValidCoord(lat, lng))
            {
                continue;
            }

            var osmId = OsmKey(el);
            if (osmId is not null && !seenOsm.Add(osmId))
            {
                continue;
            }

            var addr = Addr(el);
            var kind = KindOf(el, addr);
            var label = LabelOf(el, addr);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var coordKey =
                $"{Math.Round(lat, 4).ToString(CultureInfo.InvariantCulture)}|{Math.Round(lng, 4).ToString(CultureInfo.InvariantCulture)}|{label.ToLowerInvariant()}";
            if (!seenCoord.Add(coordKey))
            {
                continue;
            }

            var subtitle = SubtitleOf(addr, label, Str(el, "display_name"));
            var kindLabel = KindLabelOf(kind);
            rows.Add((new GeoHit(label, lat, lng, kind, subtitle, kindLabel, ChipOf(label, subtitle)), RankOf(kind)));
        }

        return rows.OrderBy(r => r.Rank).Select(r => r.Hit).Take(12).ToList();
    }

    private static string LabelOf(JsonElement el, JsonElement addr)
    {
        return FirstNonEmpty(
            Addr(addr, "neighbourhood"),
            Addr(addr, "quarter"),
            Str(el, "name"),
            Addr(addr, "suburb"),
            FirstComma(Str(el, "display_name"))) ?? "";
    }

    private static string SubtitleOf(JsonElement addr, string label, string? display)
    {
        var parts = new List<string>();
        AddPart(parts, label, Addr(addr, "suburb"));
        AddPart(parts, label, Addr(addr, "city_district"));
        AddPart(parts, label, Addr(addr, "district"));
        AddPart(parts, label, Addr(addr, "town"));
        AddPart(parts, label, Addr(addr, "city"));
        AddPart(parts, label, Addr(addr, "province"));
        AddPart(parts, label, Addr(addr, "state"));

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(display))
        {
            foreach (var seg in display.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                AddPart(parts, label, seg);
            }
        }

        return string.Join(" · ", parts.Take(3));
    }

    private static void AddPart(List<string> parts, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var v = value.Trim();
        if (v.Equals(label, StringComparison.OrdinalIgnoreCase)
            || v.Equals("Türkiye", StringComparison.OrdinalIgnoreCase)
            || v.Equals("Turkey", StringComparison.OrdinalIgnoreCase)
            || parts.Any(p => p.Equals(v, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        parts.Add(v);
    }

    private static string ChipOf(string label, string subtitle)
    {
        if (string.IsNullOrWhiteSpace(subtitle))
        {
            return label;
        }

        var first = subtitle.Split(" · ", 2, StringSplitOptions.TrimEntries)[0];
        return first.Equals(label, StringComparison.OrdinalIgnoreCase) ? label : $"{label}, {first}";
    }

    private static string KindOf(JsonElement el, JsonElement addr)
    {
        var addresstype = Str(el, "addresstype");
        if (!string.IsNullOrWhiteSpace(addresstype))
        {
            return addresstype;
        }

        var type = Str(el, "type");
        if (!string.IsNullOrWhiteSpace(type) && type is not "administrative" and not "yes")
        {
            return type;
        }

        if (Addr(addr, "neighbourhood") is not null)
        {
            return "neighbourhood";
        }

        if (Addr(addr, "suburb") is not null)
        {
            return "suburb";
        }

        if (Addr(addr, "city_district") is not null)
        {
            return "city_district";
        }

        return type ?? Str(el, "class") ?? "place";
    }

    private static string KindLabelOf(string kind) => kind switch
    {
        "neighbourhood" or "quarter" or "suburb" or "hamlet" or "isolated_dwelling" => "Mahalle",
        "city_district" or "district" or "administrative" => "İlçe",
        "town" or "municipality" => "İlçe",
        "city" or "village" => "Şehir",
        "bus_stop" or "station" or "halt" or "tram_stop" or "stop_position" or "stop_area" => "Durak",
        "road" or "pedestrian" or "residential" or "living_street" => "Cadde",
        _ => "Yer"
    };

    private static int RankOf(string kind) => kind switch
    {
        "neighbourhood" or "quarter" or "hamlet" => 0,
        "suburb" => 1,
        "city_district" or "district" => 2,
        "town" or "city" or "village" => 3,
        "administrative" => 4,
        "bus_stop" or "station" or "halt" or "tram_stop" or "stop_position" => 5,
        _ => 6
    };

    private static JsonElement Addr(JsonElement el) =>
        el.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;

    private static string? Addr(JsonElement addr, string key) =>
        addr.ValueKind == JsonValueKind.Object ? Str(addr, key) : null;

    private static string? OsmKey(JsonElement el)
    {
        var type = Str(el, "osm_type");
        var id = el.TryGetProperty("osm_id", out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.String => v.GetString(),
                _ => null
            }
            : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return $"{type}:{id}";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? FirstComma(string? display)
    {
        if (string.IsNullOrWhiteSpace(display))
        {
            return null;
        }

        var i = display.IndexOf(',');
        return i < 0 ? display.Trim() : display[..i].Trim();
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
