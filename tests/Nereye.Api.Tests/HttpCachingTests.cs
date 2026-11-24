using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Primitives;

namespace Nereye.Api.Tests;

public class EntityTagsTests
{
    [Fact]
    public void Same_body_gives_same_weak_tag_and_different_body_differs()
    {
        var a = EntityTags.Compute("{\"items\":[1]}"u8);
        var b = EntityTags.Compute("{\"items\":[1]}"u8);
        var c = EntityTags.Compute("{\"items\":[2]}"u8);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.StartsWith("W/\"", a);
        Assert.EndsWith("\"", a);
    }

    [Theory]
    [InlineData("W/\"abc\"", true)]
    [InlineData("\"abc\"", true)]
    [InlineData("\"zzz\", W/\"abc\"", true)]
    [InlineData("*", true)]
    [InlineData("\"zzz\"", false)]
    [InlineData("", false)]
    public void If_none_match_uses_weak_comparison(string header, bool expected)
    {
        Assert.Equal(expected, EntityTags.Matches(new StringValues(header), "W/\"abc\""));
    }
}

/// <summary>Saat sabit: "kapanmasına N dk" alanı istekler arasında değişmesin, gövde aynı kalsın.</summary>
public sealed class HttpCachingTests : IClassFixture<ControlledApiFactory>
{
    private const string Url = "/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500";
    private readonly ControlledApiFactory _factory;

    public HttpCachingTests(ControlledApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Recommendations_answer_304_without_body_when_tag_matches()
    {
        using var client = _factory.CreateClient();
        var first = await client.GetAsync(Url);
        first.EnsureSuccessStatusCode();
        var tag = first.Headers.ETag;
        Assert.NotNull(tag);
        Assert.True(tag.IsWeak);
        Assert.Contains("no-cache", first.Headers.CacheControl?.ToString());
        Assert.True(first.Headers.CacheControl?.Private);
        Assert.Equal("application/json", first.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"items\"", await first.Content.ReadAsStringAsync());

        using var again = new HttpRequestMessage(HttpMethod.Get, Url);
        again.Headers.IfNoneMatch.Add(tag);
        var second = await client.SendAsync(again);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(tag, second.Headers.ETag);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Changed_list_answers_200_with_a_new_tag()
    {
        using var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, Url);
        req.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"eski\"", isWeak: true));
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotEqual("W/\"eski\"", res.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Venue_detail_supports_conditional_get()
    {
        using var client = _factory.CreateClient();
        var first = await client.GetAsync("/api/venues/nereye:petra");
        first.EnsureSuccessStatusCode();
        var tag = first.Headers.ETag;
        Assert.NotNull(tag);

        using var again = new HttpRequestMessage(HttpMethod.Get, "/api/venues/nereye:petra");
        again.Headers.IfNoneMatch.Add(tag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(again)).StatusCode);
    }

    [Fact]
    public async Task Missing_venue_is_404_without_tag()
    {
        using var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/venues/nereye:yok-boyle-bir-yer");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Null(res.Headers.ETag);
    }
}

/// <summary>İstemci isteği bırakınca (niyet değişti) soğuk liste çekimi yarıda kalmaz: önbelleğe yazılır.</summary>
public sealed class AbandonedRequestTests : IClassFixture<ControlledApiFactory>
{
    private readonly ControlledApiFactory _factory;

    public AbandonedRequestTests(ControlledApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cold_fetch_finishes_and_fills_cache_after_client_gives_up()
    {
        using var client = _factory.CreateClient();
        _factory.Places.Delay = TimeSpan.FromMilliseconds(600);
        const string url = "/api/recommendations?lat=41.0430&lng=29.0070&intent=Yemek&radiusMeters=1500";
        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync(url, cts.Token));
        }

        await Task.Delay(900);
        _factory.Places.Delay = TimeSpan.Zero;
        var calls = _factory.Places.NearbyCalls;
        var res = await client.GetAsync(url);
        res.EnsureSuccessStatusCode();
        Assert.Equal(calls, _factory.Places.NearbyCalls);
    }
}
