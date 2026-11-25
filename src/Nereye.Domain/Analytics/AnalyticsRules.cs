using System.Text.RegularExpressions;

namespace Nereye.Domain.Analytics;

/// <summary>
/// Kullanım analitiği: yalnızca bilinen olaylar sayılır; kullanıcı kimliği, konum ya da serbest metin taşınmaz.
/// Mekan olaylarının anahtarı mekan kimliği, liste olayınınki niyettir. İstemcideki lib/analytics-queue.ts aynasıdır.
/// </summary>
public static partial class AnalyticsRules
{
    public const int MaxBatch = 50;

    /// <summary>Mekan kimliği isteyen olaylar.</summary>
    public static readonly string[] VenueEvents = ["venue_view", "directions", "save", "share"];

    /// <summary>Anahtarsız olaylar.</summary>
    public static readonly string[] PlainEvents = ["app_open", "decide_open", "search"];

    public const string ListView = "list_view";

    public static bool TryKey(string? name, string? placeId, string? intent, out string key)
    {
        key = "";
        if (name is null)
        {
            return false;
        }

        if (VenueEvents.Contains(name))
        {
            if (placeId is null || placeId.Length > 128 || !PlaceId().IsMatch(placeId))
            {
                return false;
            }

            key = placeId;
            return true;
        }

        if (name == ListView)
        {
            if (!Enum.TryParse<Intent>(intent, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
            {
                return false;
            }

            key = parsed.ToString();
            return true;
        }

        return PlainEvents.Contains(name);
    }

    [GeneratedRegex(@"^[A-Za-z0-9:_\-./]+$")]
    private static partial Regex PlaceId();
}

public sealed record FunnelStep(string Name, long Count, double? FromPrevious, double? FromStart);

/// <summary>Açılış → liste → mekan → yol tarifi hunisi.</summary>
public static class Funnel
{
    public static readonly string[] Steps = ["app_open", "list_view", "venue_view", "directions"];

    public static IReadOnlyList<FunnelStep> Compute(IReadOnlyDictionary<string, long> totals)
    {
        var result = new List<FunnelStep>();
        long? previous = null;
        long start = totals.GetValueOrDefault(Steps[0]);
        foreach (var name in Steps)
        {
            var count = totals.GetValueOrDefault(name);
            result.Add(new FunnelStep(
                name,
                count,
                previous is > 0 ? Math.Round((double)count / previous.Value, 4) : null,
                result.Count > 0 && start > 0 ? Math.Round((double)count / start, 4) : null));
            previous = count;
        }

        return result;
    }
}
