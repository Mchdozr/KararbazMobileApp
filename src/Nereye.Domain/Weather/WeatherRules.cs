using Nereye.Domain.Places;

namespace Nereye.Domain.Weather;

public enum WeatherKind
{
    Clear,
    Cloudy,
    Fog,
    Rain,
    Snow,
    Storm
}

public sealed record WeatherNow(WeatherKind Kind, double TemperatureC, bool IsDay);

public sealed record WeatherNote(string Text, Intent? Suggest);

/// <summary>
/// Hava durumuna göre öneri: yağışta açık hava mekanları geri düşer; güzel havada (açık/parçalı bulutlu, 15–29°, gündüz)
/// açık hava ve dış masası olan mekanlar öne çıkar. Kapalı mekanlara dokunulmaz.
/// </summary>
public static class WeatherRules
{
    public static WeatherKind FromWmo(int code) => code switch
    {
        0 or 1 => WeatherKind.Clear,
        2 or 3 => WeatherKind.Cloudy,
        45 or 48 => WeatherKind.Fog,
        >= 51 and <= 67 => WeatherKind.Rain,
        >= 80 and <= 82 => WeatherKind.Rain,
        >= 71 and <= 77 => WeatherKind.Snow,
        85 or 86 => WeatherKind.Snow,
        >= 95 and <= 99 => WeatherKind.Storm,
        _ => WeatherKind.Cloudy
    };

    public static bool IsWet(WeatherKind kind) => kind is WeatherKind.Rain or WeatherKind.Snow or WeatherKind.Storm;

    public static bool IsPleasant(WeatherNow w) =>
        w.IsDay && w.Kind is WeatherKind.Clear or WeatherKind.Cloudy && w.TemperatureC is >= 15 and <= 29;

    public static double Factor(WeatherNow? weather, IReadOnlyList<string> types, string? outdoorSeating)
    {
        if (weather is null)
        {
            return 1.0;
        }

        var outdoor = types.Count > 0 && IntentPlaceTypes.FamilyOf(types[0]) == IntentPlaceTypes.Family.Outdoor;
        var terrace = string.Equals(outdoorSeating, "yes", StringComparison.OrdinalIgnoreCase);
        if (IsWet(weather.Kind))
        {
            return outdoor ? 0.55 : 1.0;
        }

        if (outdoor && weather.TemperatureC < 5)
        {
            return 0.8;
        }

        if (IsPleasant(weather))
        {
            return outdoor ? 1.15 : terrace ? 1.08 : 1.0;
        }

        return 1.0;
    }

    public static WeatherNote? Note(WeatherNow? weather, Intent intent)
    {
        if (weather is null)
        {
            return null;
        }

        var degrees = $"{Math.Round(weather.TemperatureC):0}°";
        if (IsWet(weather.Kind))
        {
            var what = weather.Kind switch
            {
                WeatherKind.Snow => "Kar yağıyor",
                WeatherKind.Storm => "Fırtına var",
                _ => "Yağmur yağıyor"
            };
            return intent == Intent.AcikHava
                ? new WeatherNote($"{what} ({degrees}). Açık havada ıslanabilirsin; müzeler ve kapalı mekanlar daha iyi olabilir.", Intent.Kultur)
                : new WeatherNote($"{what} ({degrees}). Açık hava mekanlarını geriye, kapalı mekanları öne aldık.", null);
        }

        if (IsPleasant(weather))
        {
            return intent == Intent.AcikHava
                ? new WeatherNote($"Hava güzel ({degrees}). Açık hava için tam zamanı.", null)
                : new WeatherNote($"Hava güzel ({degrees}). Dış masası olan yerleri öne aldık; açık havaya da bakabilirsin.", Intent.AcikHava);
        }

        return null;
    }
}
