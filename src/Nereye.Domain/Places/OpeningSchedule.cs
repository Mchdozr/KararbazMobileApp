using System.Globalization;
using System.Text.RegularExpressions;

namespace Nereye.Domain.Places;

/// <summary>Bir günün açık aralığı. Bitiş 1440'ı geçebilir: gece yarısını aşan aralık (ör. 18:00–02:00).</summary>
public sealed record OpenInterval(DayOfWeek Day, int StartMinute, int EndMinute);

/// <summary>
/// "Şu an" için hesaplanmış durum. Kapanış/açılış dakikaları istemciye "23:00'e kadar açık", "Yakında kapanıyor",
/// "09:00'da açılıyor" yazdırır; saat dilimi hesabı sunucuda kalır.
/// </summary>
public sealed record OpenState(
    bool? IsOpen,
    string? ClosesAt = null,
    int? ClosesInMinutes = null,
    string? OpensAt = null,
    int? OpensInMinutes = null)
{
    public static readonly OpenState Unknown = new((bool?)null);
}

/// <summary>
/// Çalışma saatleri: OSM <c>opening_hours</c> söz dizimi ve Google'ın gün satırları ("Pazartesi: 09:00–23:00",
/// "Monday: 9:00 AM – 10:00 PM") tek modele çevrilir ve mekanın yerel saatiyle (İstanbul) değerlendirilir.
/// Sunucu UTC'de çalışır; "açık mı" hesabı sunucu saatine göre yapılırsa akşamları üç saat kayar.
/// </summary>
public static class OpeningSchedule
{
    private const int Day = 24 * 60;

    /// <summary>Türkiye tek saat dilimi; tzdata olmayan imajlarda sabit UTC+3'e düşer (yaz saati yok).</summary>
    public static readonly TimeZoneInfo Zone = FindZone();

    private static readonly (string Key, DayOfWeek Day)[] DayNames =
    [
        ("mo", DayOfWeek.Monday), ("tu", DayOfWeek.Tuesday), ("we", DayOfWeek.Wednesday), ("th", DayOfWeek.Thursday),
        ("fr", DayOfWeek.Friday), ("sa", DayOfWeek.Saturday), ("su", DayOfWeek.Sunday),
    ];

    // Google gün adları (tr ve en). Uzun adlar önce: "pazartesi" "pazar"dan önce denenir.
    private static readonly (string Name, DayOfWeek Day)[] LongDayNames =
    [
        ("pazartesi", DayOfWeek.Monday), ("salı", DayOfWeek.Tuesday), ("çarşamba", DayOfWeek.Wednesday),
        ("perşembe", DayOfWeek.Thursday), ("cumartesi", DayOfWeek.Saturday), ("cuma", DayOfWeek.Friday),
        ("pazar", DayOfWeek.Sunday), ("monday", DayOfWeek.Monday), ("tuesday", DayOfWeek.Tuesday),
        ("wednesday", DayOfWeek.Wednesday), ("thursday", DayOfWeek.Thursday), ("friday", DayOfWeek.Friday),
        ("saturday", DayOfWeek.Saturday), ("sunday", DayOfWeek.Sunday),
    ];

    private static readonly Regex Range = new(
        @"(\d{1,2})(?:[:.](\d{2}))?\s*(am|pm|öö|ös)?\s*[-–—]\s*(\d{1,2})(?:[:.](\d{2}))?\s*(am|pm|öö|ös)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static OpenState Evaluate(string? text, DateTimeOffset now)
    {
        var intervals = Parse(text);
        if (intervals.Count == 0)
        {
            return OpenState.Unknown;
        }

        var local = TimeZoneInfo.ConvertTime(now, Zone);
        var minute = local.Hour * 60 + local.Minute;
        var today = local.DayOfWeek;

        // Açık mı: bugünün aralığı ya da dünden taşan gece aralığı.
        foreach (var iv in intervals)
        {
            var offset = iv.Day == today ? 0 : iv.Day == Previous(today) ? Day : -1;
            if (offset < 0)
            {
                continue;
            }

            var t = minute + offset;
            if (t >= iv.StartMinute && t < iv.EndMinute)
            {
                // Bugünün 00:00'ına göre kapanış; gece yarısında biten aralık ertesi gün 00:00'da sürüyorsa zincirlenir.
                var end = iv.EndMinute - offset;
                for (var hops = 0; end % Day == 0; hops++)
                {
                    if (hops >= 7)
                    {
                        return new OpenState(true); // kesintisiz: kapanış yok
                    }

                    var next = (DayOfWeek)(((int)today + end / Day) % 7);
                    var cont = intervals.FirstOrDefault(x => x.Day == next && x.StartMinute == 0);
                    if (cont is null)
                    {
                        break;
                    }

                    end = end / Day * Day + cont.EndMinute;
                }

                return new OpenState(true, Clock(end), end - minute);
            }
        }

        // Kapalı: önümüzdeki yedi gün içindeki ilk açılış.
        int? best = null;
        foreach (var iv in intervals)
        {
            for (var k = 0; k <= 7; k++)
            {
                var day = (DayOfWeek)(((int)today + k) % 7);
                if (iv.Day != day)
                {
                    continue;
                }

                var wait = k * Day + iv.StartMinute - minute;
                if (wait > 0 && (best is null || wait < best))
                {
                    best = wait;
                }
            }
        }

        return best is { } w
            ? new OpenState(false, OpensAt: Clock(minute + w), OpensInMinutes: w)
            : new OpenState(false);
    }

    public static IReadOnlyList<OpenInterval> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var trimmed = text.Trim();
        if (trimmed.Contains("24/7", StringComparison.Ordinal))
        {
            return AllDays(0, Day);
        }

        return LooksLikeGoogle(trimmed) ? ParseGoogle(trimmed) : ParseOsm(trimmed);
    }

    // ---- OSM ----

    private static IReadOnlyList<OpenInterval> ParseOsm(string spec)
    {
        // Sonraki kural aynı günler için öncekini ezer ("Mo-Fr 08:00-18:00; We off").
        var byDay = new Dictionary<DayOfWeek, List<OpenInterval>>();
        // Kural ayırıcıları: ";", "||" ve bir saatten sonra gelen "," + gün ("Mo-Fr 08:00-18:00, Sa 09:00-13:00").
        var rules = Regex.Split(spec, @";|\|\||(?<=\d)\s*,\s*(?=(?:Mo|Tu|We|Th|Fr|Sa|Su|PH|SH)\b)", RegexOptions.IgnoreCase);
        foreach (var rawRule in rules.Select(r => r.Trim()).Where(r => r.Length > 0))
        {
            var rule = rawRule.Trim();
            if (rule.StartsWith("PH", StringComparison.OrdinalIgnoreCase) || rule.StartsWith("SH", StringComparison.OrdinalIgnoreCase))
            {
                continue; // resmi tatil/okul tatili kuralı: bugünün tatil olup olmadığını bilmiyoruz
            }

            var firstDigit = rule.IndexOfAny("0123456789".ToCharArray());
            var head = firstDigit < 0 ? rule : rule[..firstDigit];
            var off = Regex.IsMatch(rule, @"\b(off|closed)\b", RegexOptions.IgnoreCase);
            var daysPart = Regex.Replace(head, @"\b(off|closed)\b", "", RegexOptions.IgnoreCase).Trim().TrimEnd(':').Trim();
            if (Regex.IsMatch(daysPart, @"[A-Za-z]") && !Regex.IsMatch(daysPart, @"^(?:(?:Mo|Tu|We|Th|Fr|Sa|Su)(?:\s*-\s*(?:Mo|Tu|We|Th|Fr|Sa|Su))?\s*,?\s*)+$", RegexOptions.IgnoreCase))
            {
                continue; // ay/hafta seçici ya da tanınmayan ifade: yanlış "açık" demektense kuralı atla
            }

            var days = daysPart.Length == 0 ? AllWeek() : ExpandOsmDays(daysPart);
            if (days.Count == 0)
            {
                continue;
            }

            var ranges = off ? [] : TimeRanges(firstDigit < 0 ? "" : rule[firstDigit..]);
            if (!off && ranges.Count == 0)
            {
                continue;
            }

            foreach (var d in days)
            {
                byDay[d] = ranges.Select(r => new OpenInterval(d, r.Start, r.End)).ToList();
            }
        }

        return byDay.Values.SelectMany(x => x).ToList();
    }

    private static List<DayOfWeek> ExpandOsmDays(string token)
    {
        var result = new List<DayOfWeek>();
        foreach (var part in token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = part.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (range.Length == 1 && TryOsmDay(range[0], out var one))
            {
                result.Add(one);
            }
            else if (range.Length == 2 && TryOsmDay(range[0], out var from) && TryOsmDay(range[1], out var to))
            {
                for (var d = from; ; d = (DayOfWeek)(((int)d + 1) % 7))
                {
                    result.Add(d);
                    if (d == to)
                    {
                        break;
                    }
                }
            }
        }

        return result.Distinct().ToList();
    }

    private static bool TryOsmDay(string token, out DayOfWeek day)
    {
        foreach (var (key, d) in DayNames)
        {
            if (token.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                day = d;
                return true;
            }
        }

        day = default;
        return false;
    }

    // ---- Google gün satırları ----

    private static bool LooksLikeGoogle(string text) =>
        LongDayNames.Any(n => text.Contains(n.Name + ":", StringComparison.OrdinalIgnoreCase)
                              || text.Contains(Cap(n.Name) + ":", StringComparison.Ordinal));

    private static IReadOnlyList<OpenInterval> ParseGoogle(string text)
    {
        var result = new List<OpenInterval>();
        foreach (var line in text.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var name = line[..colon].Trim().ToLower(new CultureInfo("tr-TR"));
            var dayHit = LongDayNames.FirstOrDefault(n => name == n.Name || name == n.Name.ToLowerInvariant());
            if (dayHit.Name is null)
            {
                continue;
            }

            var spec = line[(colon + 1)..].Trim();
            var lower = spec.ToLower(new CultureInfo("tr-TR"));
            if (lower.Contains("24 saat") || lower.Contains("open 24 hours"))
            {
                result.Add(new OpenInterval(dayHit.Day, 0, Day));
                continue;
            }

            if (lower.Contains("kapalı") || lower.Contains("closed"))
            {
                continue;
            }

            result.AddRange(TimeRanges(spec).Select(r => new OpenInterval(dayHit.Day, r.Start, r.End)));
        }

        return result;
    }

    // ---- ortak ----

    private static List<(int Start, int End)> TimeRanges(string text)
    {
        var list = new List<(int, int)>();
        // Dar boşluk ve benzeri ayırıcılar (Google "9:00 AM" içinde U+202F kullanır).
        var clean = text.Replace(' ', ' ').Replace(' ', ' ').Replace(' ', ' ');
        foreach (Match m in Range.Matches(clean))
        {
            var start = ToMinute(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
            var endMeridiem = m.Groups[6].Value;
            // "9:00 – 10:00 PM": başta işaret yoksa sondakini başlangıca da uygulama, ama 12 saat düzeni varsa ayrıca bak.
            var end = ToMinute(m.Groups[4].Value, m.Groups[5].Value, endMeridiem);
            if (start is null || end is null)
            {
                continue;
            }

            var e = end.Value;
            if (e <= start.Value)
            {
                e += Day; // gece yarısını aşıyor (18:00–02:00) ya da 00:00 kapanış
            }

            list.Add((start.Value, e));
        }

        return list;
    }

    private static int? ToMinute(string h, string m, string meridiem)
    {
        if (!int.TryParse(h, NumberStyles.None, CultureInfo.InvariantCulture, out var hour))
        {
            return null;
        }

        var minute = string.IsNullOrEmpty(m) ? 0 : int.Parse(m, CultureInfo.InvariantCulture);
        var mer = meridiem.ToLowerInvariant();
        if (mer is "pm" or "ös" && hour < 12)
        {
            hour += 12;
        }
        else if (mer is "am" or "öö" && hour == 12)
        {
            hour = 0;
        }

        if (hour > 24 || minute > 59 || (hour == 24 && minute > 0))
        {
            return null;
        }

        return hour * 60 + minute;
    }

    private static IReadOnlyList<OpenInterval> AllDays(int start, int end) =>
        Enum.GetValues<DayOfWeek>().Select(d => new OpenInterval(d, start, end)).ToList();

    private static List<DayOfWeek> AllWeek() => Enum.GetValues<DayOfWeek>().ToList();

    private static DayOfWeek Previous(DayOfWeek d) => (DayOfWeek)(((int)d + 6) % 7);

    private static string Clock(int minute)
    {
        var m = ((minute % Day) + Day) % Day;
        return $"{m / 60:00}:{m % 60:00}";
    }

    private static string Cap(string s) => char.ToUpper(s[0], new CultureInfo("tr-TR")) + s[1..];

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in (string[])["Europe/Istanbul", "Turkey Standard Time"])
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // sıradaki ad
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Nereye/Istanbul", TimeSpan.FromHours(3), "İstanbul", "İstanbul");
    }
}

/// <summary>Önbellekteki "açık" bayrağı veri çekildiği ana aittir; sunum anında saat metninden yeniden hesaplanır.</summary>
public static class OpenNowResolver
{
    public static (PlaceSnapshot Place, OpenState State) Apply(PlaceSnapshot place, DateTimeOffset now)
    {
        var state = OpeningSchedule.Evaluate(place.OpeningHoursText, now);
        if (state.IsOpen is null)
        {
            return (place, new OpenState(place.OpenNow));
        }

        return (place.OpenNow == state.IsOpen ? place : place with { OpenNow = state.IsOpen }, state);
    }
}
