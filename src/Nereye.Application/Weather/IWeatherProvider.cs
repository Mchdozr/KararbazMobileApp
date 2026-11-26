using Nereye.Domain.Weather;

namespace Nereye.Application.Weather;

/// <summary>Anlık hava. Bilinmiyorsa null: öneriler havasız devam eder.</summary>
public interface IWeatherProvider
{
    Task<WeatherNow?> CurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

/// <summary>Source: atıf için veri kaynağı (ör. "MET Norway"); istemci notun altında gösterir.</summary>
public sealed record WeatherDto(string Kind, double TemperatureC, string? Note, string? SuggestIntent, string? Source = null);
