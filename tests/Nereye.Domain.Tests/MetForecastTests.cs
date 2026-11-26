using Nereye.Domain.Weather;

namespace Nereye.Domain.Tests;

public class MetForecastTests
{
    // api.met.no Locationforecast 2.0 /compact yanıtından kısaltılmış gerçek örnek (Kadıköy, 2026-11-26).
    private const string Body = """
        {"type":"Feature","geometry":{"type":"Point","coordinates":[29.025,40.9885,12]},
         "properties":{"meta":{"updated_at":"2026-11-26T07:17:51Z","units":{"air_temperature":"celsius"}},
          "timeseries":[
           {"time":"2026-11-26T07:00:00Z","data":{"instant":{"details":{"air_temperature":8.0}},
             "next_12_hours":{"summary":{"symbol_code":"partlycloudy_day"},"details":{}},
             "next_1_hours":{"summary":{"symbol_code":"partlycloudy_day"},"details":{"precipitation_amount":0.0}}}},
           {"time":"2026-11-26T08:00:00Z","data":{"instant":{"details":{"air_temperature":9.5}},
             "next_1_hours":{"summary":{"symbol_code":"lightrain"},"details":{"precipitation_amount":0.4}}}},
           {"time":"2026-11-26T09:00:00Z","data":{"instant":{"details":{"air_temperature":10.8}},
             "next_1_hours":{"summary":{"symbol_code":"heavyrainshowersandthunder_day"},"details":{}}}},
           {"time":"2026-11-29T00:00:00Z","data":{"instant":{"details":{"air_temperature":6.1}},
             "next_6_hours":{"summary":{"symbol_code":"cloudy"},"details":{}}}}
          ]}}
        """;

    private static DateTime Utc(string s) => DateTime.Parse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

    [Theory]
    [InlineData("clearsky_day", WeatherKind.Clear)]
    [InlineData("fair_night", WeatherKind.Clear)]
    [InlineData("partlycloudy_polartwilight", WeatherKind.Cloudy)]
    [InlineData("cloudy", WeatherKind.Cloudy)]
    [InlineData("fog", WeatherKind.Fog)]
    [InlineData("lightrain", WeatherKind.Rain)]
    [InlineData("heavyrainshowers_day", WeatherKind.Rain)]
    [InlineData("sleet", WeatherKind.Rain)]
    [InlineData("lightsnowshowers_night", WeatherKind.Snow)]
    [InlineData("rainandthunder", WeatherKind.Storm)]
    [InlineData("lightssnowshowersandthunder_day", WeatherKind.Storm)]
    [InlineData("", WeatherKind.Cloudy)]
    [InlineData("bilinmeyen", WeatherKind.Cloudy)]
    public void Symbol_codes_map_to_kinds(string code, WeatherKind kind) => Assert.Equal(kind, WeatherRules.FromMetSymbol(code));

    [Fact]
    public void Parses_only_hourly_steps()
    {
        var hours = MetForecastJson.Parse(Body);
        Assert.Equal(3, hours.Count);
        Assert.Equal(new MetHour(Utc("2026-11-26T08:00:00Z"), 9.5, "lightrain"), hours[1]);
    }

    [Fact]
    public void Picks_the_hour_that_contains_now()
    {
        var hours = MetForecastJson.Parse(Body);
        Assert.Equal("lightrain", MetForecastJson.At(hours, Utc("2026-11-26T08:59:00Z"))!.Symbol);
        Assert.Equal("partlycloudy_day", MetForecastJson.At(hours, Utc("2026-11-26T07:00:00Z"))!.Symbol);
        // Seri dışı: tahmin yok demek, uydurulmaz.
        Assert.Null(MetForecastJson.At(hours, Utc("2026-11-26T06:59:00Z")));
        Assert.Null(MetForecastJson.At(hours, Utc("2026-11-26T10:00:00Z")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{"properties":{"timeseries":[{"time":"x","data":{}}]}}""")]
    [InlineData("not json")]
    public void Broken_body_yields_no_hours(string body) => Assert.Empty(MetForecastJson.Parse(body));

    [Theory]
    // İstanbul (41.0, 29.0). Yerel saat UTC+3.
    [InlineData("2026-06-21T09:00:00Z", true)]   // öğle
    [InlineData("2026-06-21T21:00:00Z", false)]  // gece yarısı
    [InlineData("2026-12-21T05:00:00Z", false)]  // 08:00, gün doğumundan (~08:25) önce
    [InlineData("2026-12-21T06:00:00Z", true)]   // 09:00
    [InlineData("2026-12-21T15:00:00Z", false)]  // 18:00, gün batımından (~17:40, kalıcı UTC+3) sonra
    [InlineData("2026-12-21T14:20:00Z", true)]   // 17:20
    public void Day_and_night_follow_the_sun(string utc, bool day) =>
        Assert.Equal(day, SunPosition.IsDay(41.0, 29.0, Utc(utc)));
}
