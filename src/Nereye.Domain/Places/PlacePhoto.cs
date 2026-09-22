using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nereye.Domain.Places;

public static partial class PlacePhoto
{
    [GeneratedRegex(@"<meta[^>]+(?:property|name)\s*=\s*[""']og:image[""'][^>]*content\s*=\s*[""']([^""']+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgImageMeta();

    [GeneratedRegex(@"<meta[^>]+content\s*=\s*[""']([^""']+)[""'][^>]*(?:property|name)\s*=\s*[""']og:image[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgImageMetaRev();

    private static readonly HashSet<string> GenericTitleWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "cafe", "kahve", "coffee", "bar", "pub", "restaurant", "restoran", "shop", "store",
        "the", "and", "istanbul", "street", "caddesi", "sokak", "avenue",
        "park", "market", "book",
    };

    public static string? FromNominatim(JsonElement el)
    {
        if (!el.TryGetProperty("extratags", out var extra) || extra.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return FromHttpOrCommons(Extra(extra, "image"), Extra(extra, "wikimedia_commons"));
    }

    public static string? FromHttpOrCommons(string? image, string? commons)
    {
        return FirstTrusted([image, CommonsFileUrl(commons)]);
    }

    public static string? FirstTrusted(IEnumerable<string?> candidates)
    {
        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw) || LooksLikeSelfieOrPortrait(raw))
            {
                continue;
            }

            if (raw.StartsWith("places/", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("/api/media/", StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }

            if (IsHttp(raw))
            {
                return raw;
            }

            var commons = CommonsFileUrl(raw);
            if (commons is not null && !LooksLikeSelfieOrPortrait(raw))
            {
                return commons;
            }
        }

        return null;
    }

    public static bool LooksLikeSelfieOrPortrait(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var lower = value.ToLowerInvariant();
        return lower.Contains("portrait", StringComparison.Ordinal)
               || lower.Contains("selfie", StringComparison.Ordinal)
               || lower.Contains("mugshot", StringComparison.Ordinal);
    }

    public static string? ExtraFromNominatim(JsonElement el, params string[] keys)
    {
        if (!el.TryGetProperty("extratags", out var extra) || extra.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (extra.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }

        return null;
    }

    public static string? WebsiteFromNominatim(JsonElement el) =>
        ExtraObject(el, out var extra) ? FirstHttp(extra, "website", "contact:website", "url") : null;

    public static string? WikidataFromNominatim(JsonElement el)
    {
        if (!ExtraObject(el, out var extra))
        {
            return null;
        }

        var id = Extra(extra, "wikidata");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        id = id.Trim();
        return id.Length > 1 && id[0] is 'Q' or 'q' && id[1..].All(char.IsDigit) ? id.ToUpperInvariant() : null;
    }

    public readonly record struct CommonsGeoHit(string? Title, double Dist);

    public static string? PickCommonsNearby(IEnumerable<CommonsGeoHit> rows, string venueName)
    {
        foreach (var row in rows)
        {
            var url = CommonsFileUrl(row.Title);
            if (url is not null && TitleFitsVenue(venueName, row.Title))
            {
                return url;
            }
        }

        return null;
    }

    public static string? FirstUnused(IEnumerable<string?> candidates, ConcurrentDictionary<string, byte> claimed)
    {
        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (claimed.TryAdd(raw, 0))
            {
                return raw;
            }
        }

        return null;
    }

    public static string? CommonsFileUrl(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var name = title.Trim();
        if (name.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
        {
            name = name[5..];
        }

        if (name.StartsWith("Category:", StringComparison.OrdinalIgnoreCase) || !IsImageFile(name))
        {
            return null;
        }

        return "https://commons.wikimedia.org/wiki/Special:FilePath/"
               + Uri.EscapeDataString(name.Replace(' ', '_'))
               + "?width=800";
    }

    public static bool IsImageFile(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png") || lower.EndsWith(".webp");
    }

    public static bool TitleFitsVenue(string venueName, string? fileTitle)
    {
        if (string.IsNullOrWhiteSpace(venueName) || string.IsNullOrWhiteSpace(fileTitle))
        {
            return false;
        }

        var hay = fileTitle.Replace('_', ' ');
        if (venueName.Length >= 5 && hay.Contains(venueName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var hits = 0;
        var longHit = false;
        foreach (var raw in venueName.Split([' ', '-', '/', '&', ',', '\'', '"'], StringSplitOptions.RemoveEmptyEntries))
        {
            var word = raw.Trim();
            if (word.Length < 4 || GenericTitleWords.Contains(word))
            {
                continue;
            }

            if (!hay.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hits++;
            if (word.Length >= 6)
            {
                longHit = true;
            }
        }

        return longHit || hits >= 2;
    }

    public static bool LooksLikePersonFile(string? fileTitle)
    {
        if (string.IsNullOrWhiteSpace(fileTitle))
        {
            return true;
        }

        var name = fileTitle.Trim();
        if (name.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
        {
            name = name[5..];
        }

        var lower = name.ToLowerInvariant();
        if (lower.Contains("portrait", StringComparison.Ordinal)
            || lower.Contains("selfie", StringComparison.Ordinal)
            || lower.Contains("mugshot", StringComparison.Ordinal))
        {
            return true;
        }

        var dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            name = name[..dot];
        }

        var words = name.Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length is 1 or 2
               && words.All(w => w.Length >= 2 && w.All(char.IsLetter));
    }

    public static string? FromWikidataClaims(JsonElement json)
    {
        if (!json.TryGetProperty("claims", out var claims)
            || !claims.TryGetProperty("P18", out var p18)
            || p18.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var row in p18.EnumerateArray())
        {
            if (!row.TryGetProperty("mainsnak", out var snak)
                || !snak.TryGetProperty("datavalue", out var dv)
                || !dv.TryGetProperty("value", out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var title = value.GetString();
            if (LooksLikeSelfieOrPortrait(title))
            {
                continue;
            }

            var url = CommonsFileUrl(title);
            if (url is not null)
            {
                return url;
            }
        }

        return null;
    }

    public static string? TryOgImage(string html, string pageUrl)
    {
        if (string.IsNullOrWhiteSpace(html) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out var page))
        {
            return null;
        }

        var match = OgImageMeta().Match(html);
        if (!match.Success)
        {
            match = OgImageMetaRev().Match(html);
        }

        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups[1].Value.Trim().Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);
        if (!Uri.TryCreate(page, raw, out var abs) || !IsHttp(abs.ToString()))
        {
            return null;
        }

        return abs.ToString();
    }

    public static bool IsHttp(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

    private static bool ExtraObject(JsonElement el, out JsonElement extra)
    {
        extra = default;
        return el.TryGetProperty("extratags", out extra) && extra.ValueKind == JsonValueKind.Object;
    }

    private static string? FirstHttp(JsonElement extra, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = Extra(extra, key);
            if (IsHttp(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? Extra(JsonElement extra, string key) =>
        extra.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
