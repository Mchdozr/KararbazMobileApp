namespace Nereye.Domain.Places;

public static class OsmOpeningHours
{
    private static readonly Dictionary<string, DayOfWeek> Days = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mo"] = DayOfWeek.Monday,
        ["tu"] = DayOfWeek.Tuesday,
        ["we"] = DayOfWeek.Wednesday,
        ["th"] = DayOfWeek.Thursday,
        ["fr"] = DayOfWeek.Friday,
        ["sa"] = DayOfWeek.Saturday,
        ["su"] = DayOfWeek.Sunday,
    };

    public static IReadOnlyList<PlaceHour> Parse(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return [];
        }

        var trimmed = spec.Trim();
        if (trimmed.Contains("24/7", StringComparison.OrdinalIgnoreCase))
        {
            return Enum.GetValues<DayOfWeek>()
                .Select(d => new PlaceHour(d, TimeOnly.MinValue, new TimeOnly(23, 59)))
                .ToList();
        }

        var hours = new List<PlaceHour>();
        foreach (var raw in trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            hours.AddRange(ParseRule(raw));
        }

        return hours;
    }

    public static bool? IsOpen(string? spec, DateTimeOffset now)
    {
        var hours = Parse(spec);
        if (hours.Count == 0)
        {
            return null;
        }

        var t = TimeOnly.FromTimeSpan(now.TimeOfDay);
        return hours.Any(h => h.Day == now.DayOfWeek && t >= h.Open && t <= h.Close);
    }

    private static IEnumerable<PlaceHour> ParseRule(string rule)
    {
        var off = rule.IndexOf("off", StringComparison.OrdinalIgnoreCase);
        if (off >= 0)
        {
            yield break;
        }

        var timeAt = rule.LastIndexOf(' ');
        if (timeAt <= 0 || !TryTimes(rule[(timeAt + 1)..], out var open, out var close))
        {
            yield break;
        }

        foreach (var day in ExpandDays(rule[..timeAt].Trim()))
        {
            yield return new PlaceHour(day, open, close);
        }
    }

    private static IEnumerable<DayOfWeek> ExpandDays(string token)
    {
        foreach (var part in token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = part.Split('-', StringSplitOptions.TrimEntries);
            if (range.Length == 1 && Days.TryGetValue(range[0], out var one))
            {
                yield return one;
                continue;
            }

            if (range.Length == 2
                && Days.TryGetValue(range[0], out var from)
                && Days.TryGetValue(range[1], out var to))
            {
                var d = from;
                while (true)
                {
                    yield return d;
                    if (d == to)
                    {
                        break;
                    }

                    d = (DayOfWeek)(((int)d + 1) % 7);
                }
            }
        }
    }

    private static bool TryTimes(string span, out TimeOnly open, out TimeOnly close)
    {
        open = default;
        close = default;
        var parts = span.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        return TimeOnly.TryParse(parts[0], out open) && TimeOnly.TryParse(parts[1], out close);
    }
}
