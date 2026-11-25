using Nereye.Domain.Places;

namespace Nereye.Domain.Events;

/// <summary>
/// Mekan etkinlikleri. "Bu akşam" penceresi İstanbul saatiyle şu andan ertesi sabah 04:00'e kadardır
/// (gece yarısından sonra açılan uygulama aynı geceyi görür).
/// </summary>
public static class EventRules
{
    public const int TitleMin = 3;
    public const int TitleMax = 80;
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(12);
    public static readonly TimeSpan MaxAhead = TimeSpan.FromDays(90);

    /// <summary>Bitişi verilmemiş etkinlik başladıktan bu kadar sonra "sürüyor" sayılır.</summary>
    public static readonly TimeSpan OpenEndedGrace = TimeSpan.FromHours(2);

    private const int NightEndsAtHour = 4;

    public static (DateTime FromUtc, DateTime ToUtc) TonightWindow(DateTime nowUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), OpeningSchedule.Zone);
        var endLocal = local.Hour < NightEndsAtHour
            ? local.Date.AddHours(NightEndsAtHour)
            : local.Date.AddDays(1).AddHours(NightEndsAtHour);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), OpeningSchedule.Zone);
        return (DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), endUtc);
    }

    public static bool IsTonight(DateTime startsAtUtc, DateTime? endsAtUtc, DateTime nowUtc)
    {
        var (from, to) = TonightWindow(nowUtc);
        if (startsAtUtc >= to)
        {
            return false;
        }

        var end = endsAtUtc ?? startsAtUtc + OpenEndedGrace;
        return end > from;
    }

    public static bool IsLive(DateTime startsAtUtc, DateTime? endsAtUtc, DateTime nowUtc) =>
        startsAtUtc <= nowUtc && (endsAtUtc ?? startsAtUtc + OpenEndedGrace) > nowUtc;

    /// <summary>Hata metni (Türkçe) ya da geçerliyse null.</summary>
    public static string? Validate(string? title, DateTime startsAtUtc, DateTime? endsAtUtc, DateTime nowUtc)
    {
        var t = title?.Trim() ?? "";
        if (t.Length is < TitleMin or > TitleMax)
        {
            return $"Etkinlik adı {TitleMin}–{TitleMax} karakter olmalı.";
        }

        if (startsAtUtc < nowUtc.AddMinutes(-5))
        {
            return "Başlangıç geçmişte olamaz.";
        }

        if (startsAtUtc > nowUtc + MaxAhead)
        {
            return "En fazla 90 gün sonrası için etkinlik eklenebilir.";
        }

        if (endsAtUtc is { } end)
        {
            if (end <= startsAtUtc)
            {
                return "Bitiş başlangıçtan sonra olmalı.";
            }

            if (end - startsAtUtc > MaxDuration)
            {
                return "Etkinlik en fazla 12 saat sürebilir.";
            }
        }

        return null;
    }
}
