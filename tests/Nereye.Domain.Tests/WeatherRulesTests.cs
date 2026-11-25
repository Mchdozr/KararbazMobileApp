using Nereye.Domain.Weather;

namespace Nereye.Domain.Tests;

public class WeatherRulesTests
{
    [Theory]
    [InlineData(0, WeatherKind.Clear)]
    [InlineData(2, WeatherKind.Cloudy)]
    [InlineData(45, WeatherKind.Fog)]
    [InlineData(61, WeatherKind.Rain)]
    [InlineData(81, WeatherKind.Rain)]
    [InlineData(73, WeatherKind.Snow)]
    [InlineData(95, WeatherKind.Storm)]
    [InlineData(999, WeatherKind.Cloudy)]
    public void Wmo_codes_map_to_kinds(int code, WeatherKind kind) => Assert.Equal(kind, WeatherRules.FromWmo(code));

    private static readonly WeatherNow Rain = new(WeatherKind.Rain, 12, true);
    private static readonly WeatherNow Nice = new(WeatherKind.Clear, 22, true);

    [Fact]
    public void Rain_demotes_outdoor_places_and_leaves_indoor_alone()
    {
        Assert.True(WeatherRules.Factor(Rain, ["park"], null) < 0.7);
        Assert.Equal(1.0, WeatherRules.Factor(Rain, ["cafe"], "yes"));
        Assert.Equal(1.0, WeatherRules.Factor(Rain, ["museum"], null));
    }

    [Fact]
    public void Pleasant_weather_lifts_outdoor_places_and_terraces()
    {
        Assert.True(WeatherRules.Factor(Nice, ["park"], null) > 1.1);
        Assert.True(WeatherRules.Factor(Nice, ["cafe"], "yes") > 1.0);
        Assert.Equal(1.0, WeatherRules.Factor(Nice, ["cafe"], null));
        // Soğuk gece açık hava öne çıkmaz.
        Assert.False(WeatherRules.Factor(Nice with { IsDay = false, TemperatureC = 9 }, ["park"], null) > 1.0);
    }

    [Fact]
    public void Unknown_weather_changes_nothing() => Assert.Equal(1.0, WeatherRules.Factor(null, ["park"], null));

    [Fact]
    public void Notes_are_turkish_and_suggest_the_right_intent()
    {
        var rainy = WeatherRules.Note(Rain, Intent.AcikHava);
        Assert.NotNull(rainy);
        Assert.Contains("Yağmur", rainy.Text);
        Assert.Equal(Intent.Kultur, rainy.Suggest);

        var rainyCoffee = WeatherRules.Note(Rain, Intent.Kahve);
        Assert.Contains("kapalı", rainyCoffee!.Text);
        Assert.Null(rainyCoffee.Suggest);

        var nice = WeatherRules.Note(Nice, Intent.Kahve);
        Assert.Contains("22°", nice!.Text);
        Assert.Equal(Intent.AcikHava, nice.Suggest);
        Assert.Null(WeatherRules.Note(Nice, Intent.AcikHava)!.Suggest);

        Assert.Null(WeatherRules.Note(new WeatherNow(WeatherKind.Cloudy, 11, true), Intent.Kahve));
        Assert.Null(WeatherRules.Note(null, Intent.Kahve));
    }
}
