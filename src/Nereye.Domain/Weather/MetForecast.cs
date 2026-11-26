using System.Globalization;
using System.Text.Json;

namespace Nereye.Domain.Weather;

/// <summary>Saatlik tahmin adımı: Time'dan başlayan bir saat için sıcaklık ve MET simge kodu.</summary>
public sealed record MetHour(DateTime Time, double TemperatureC, string Symbol);

/// <summary>
/// MET Norway Locationforecast 2.0 (/compact) yanıtının ayrıştırılması. Yalnızca saatlik adımlar
/// (next_1_hours) alınır; 6 saatlik uzak adımlar "şu an" için kullanılmaz.
/// </summary>
public static class MetForecastJson
{
    /// <summary>En çok bu kadar saat saklanır (önbellekteki kayıt küçük kalsın).</summary>
    public const int MaxHours = 30;

    public static IReadOnlyList<MetHour> Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("properties", out var props)
                || !props.TryGetProperty("timeseries", out var series)
                || series.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var hours = new List<MetHour>();
            foreach (var step in series.EnumerateArray())
            {
                if (hours.Count >= MaxHours)
                {
                    break;
                }

                if (!step.TryGetProperty("time", out var timeEl)
                    || !DateTime.TryParse(timeEl.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time)
                    || !step.TryGetProperty("data", out var data)
                    || !data.TryGetProperty("next_1_hours", out var next)
                    || !next.TryGetProperty("summary", out var summary)
                    || !summary.TryGetProperty("symbol_code", out var symbol)
                    || !data.TryGetProperty("instant", out var instant)
                    || !instant.TryGetProperty("details", out var details)
                    || !details.TryGetProperty("air_temperature", out var temp)
                    || temp.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                hours.Add(new MetHour(time, temp.GetDouble(), symbol.GetString() ?? ""));
            }

            return hours;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Anı içeren saatlik adım; seri dışındaysa null (tahmin uydurulmaz).</summary>
    public static MetHour? At(IReadOnlyList<MetHour> hours, DateTime utc)
    {
        foreach (var hour in hours)
        {
            if (utc >= hour.Time && utc < hour.Time.AddHours(1))
            {
                return hour;
            }
        }

        return null;
    }
}

/// <summary>
/// Güneşin ufka göre yüksekliğinden gündüz/gece (NOAA'nın basitleştirilmiş formülü, dakika düzeyinde doğru).
/// MET simge kodunda her zaman _day/_night eki olmadığı için gündüz bilgisi buradan hesaplanır.
/// </summary>
public static class SunPosition
{
    private const double Rad = Math.PI / 180;

    public static bool IsDay(double latitude, double longitude, DateTime utc) => ElevationDegrees(latitude, longitude, utc) > -0.833;

    public static double ElevationDegrees(double latitude, double longitude, DateTime utc)
    {
        var n = (utc.ToUniversalTime() - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;
        var meanLongitude = Normalize(280.460 + 0.9856474 * n);
        var meanAnomaly = Normalize(357.528 + 0.9856003 * n) * Rad;
        var eclipticLongitude = (meanLongitude + 1.915 * Math.Sin(meanAnomaly) + 0.020 * Math.Sin(2 * meanAnomaly)) * Rad;
        var obliquity = (23.439 - 0.0000004 * n) * Rad;
        var declination = Math.Asin(Math.Sin(obliquity) * Math.Sin(eclipticLongitude));
        var rightAscension = Math.Atan2(Math.Cos(obliquity) * Math.Sin(eclipticLongitude), Math.Cos(eclipticLongitude)) / Rad;
        var siderealDegrees = Normalize(280.46061837 + 360.98564736629 * n + longitude);
        var hourAngle = (siderealDegrees - rightAscension) * Rad;
        var lat = latitude * Rad;
        var sinElevation = Math.Sin(lat) * Math.Sin(declination) + Math.Cos(lat) * Math.Cos(declination) * Math.Cos(hourAngle);
        return Math.Asin(Math.Clamp(sinElevation, -1, 1)) / Rad;
    }

    private static double Normalize(double degrees)
    {
        var d = degrees % 360;
        return d < 0 ? d + 360 : d;
    }
}
