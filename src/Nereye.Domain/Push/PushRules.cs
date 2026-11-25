using System.Globalization;
using System.Text.RegularExpressions;
using Nereye.Domain.Places;

namespace Nereye.Domain.Push;

/// <summary>
/// Bildirim kuralları. Yalnızca açık rızayla (cihaz kaydı) ve yalnızca kaydedilen mekanlardaki etkinlik için;
/// başlamadan 30 dk – 3 sa önce, etkinlik başına bir kez, cihaz başına günde en fazla 3.
/// </summary>
public static partial class PushRules
{
    public const int DailyCap = 3;
    public static readonly TimeSpan RemindFrom = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RemindUntil = TimeSpan.FromHours(3);

    public static bool IsExpoToken(string? token) => token is { Length: <= 200 } && ExpoToken().IsMatch(token);

    public static bool ShouldRemind(DateTime startsAtUtc, DateTime nowUtc)
    {
        var ahead = startsAtUtc - nowUtc;
        return ahead >= RemindFrom && ahead <= RemindUntil;
    }

    public static bool UnderDailyCap(int sentToday) => sentToday < DailyCap;

    public static (string Title, string Body) EventReminder(string eventTitle, string venueName, DateTime startsAtUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc), OpeningSchedule.Zone);
        var hhmm = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        return ($"Bu akşam: {eventTitle}", $"{venueName} · {TurkishTime.Locative(hhmm)} başlıyor");
    }

    [GeneratedRegex(@"^Expo(nent)?PushToken\[[A-Za-z0-9_\-]+\]$")]
    private static partial Regex ExpoToken();
}

/// <summary>Saatin okunuşuna göre ek (istemcideki lib/open-status.ts aynası): 20:30'da, 04:00'te, 13:00'te.</summary>
public static class TurkishTime
{
    private static readonly string[] Units = ["sıfır", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz"];
    private static readonly Dictionary<int, string> Tens = new() { [1] = "on", [2] = "yirmi", [3] = "otuz", [4] = "kırk", [5] = "elli" };
    private const string Back = "aıou";
    private const string Vowels = "aıoueiöü";
    private const string Hard = "çfhkpsşt";

    public static string Locative(string hhmm)
    {
        var word = SpokenLast(hhmm);
        if (word is null)
        {
            return hhmm;
        }

        var back = Back.Contains(LastVowel(word));
        var hard = Hard.Contains(word[^1]);
        return $"{hhmm}'{(hard ? 't' : 'd')}{(back ? 'a' : 'e')}";
    }

    private static string? SpokenLast(string hhmm)
    {
        var parts = hhmm.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute))
        {
            return null;
        }

        var n = minute == 0 ? hour % 24 : minute;
        if (n == 0)
        {
            return "sıfır";
        }

        var unit = n % 10;
        return unit > 0 ? Units[unit] : Tens.GetValueOrDefault(n / 10);
    }

    private static char LastVowel(string word)
    {
        for (var i = word.Length - 1; i >= 0; i--)
        {
            if (Vowels.Contains(word[i]))
            {
                return word[i];
            }
        }

        return 'e';
    }
}
