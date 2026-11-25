using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nereye.Api.Tests;

/// <summary>Arkadaşlarla karar ver: kısa liste oylaması (uygulama ve web sayfası).</summary>
public sealed class PollTests : IClassFixture<ControlledApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ControlledApiFactory _factory;

    public PollTests(ControlledApiFactory factory) => _factory = factory;

    private static object Option(string id, string name, string? photo = "/media/venues/petra.jpg") =>
        new { placeId = id, name, category = "Kahve dükkanı", photoUrl = photo, latitude = 40.9885, longitude = 29.025 };

    private async Task<JsonElement> CreateAsync(HttpClient client, params object[] options)
    {
        var res = await client.PostAsJsonAsync("/api/polls", new { title = "Bu akşam kahve", options });
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, raw);
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }

    private static async Task<JsonElement> VoteAsync(HttpClient client, string code, string voter, string placeId)
    {
        var res = await client.PostAsJsonAsync($"/api/polls/{code}/votes", new { voterKey = voter, placeId });
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, raw);
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }

    [Fact]
    public async Task Friends_vote_change_their_vote_and_a_winner_emerges()
    {
        using var client = _factory.CreateClient();
        var poll = await CreateAsync(client, Option("nereye:petra", "Petra"), Option("nereye:walter", "Walter's"), Option("nereye:kronotrop", "Kronotrop"));
        var code = poll.GetProperty("code").GetString()!;
        Assert.Equal(3, poll.GetProperty("options").GetArrayLength());
        Assert.Equal($"nereyego://poll/{code}", poll.GetProperty("appUrl").GetString());
        Assert.EndsWith($"/k/{code}", poll.GetProperty("webUrl").GetString());
        Assert.Equal(JsonValueKind.Null, poll.GetProperty("winnerPlaceId").ValueKind);

        // Her yeni oyun yanıtı sayıyı tam bir artırır (çift sayım yok).
        Assert.Equal(1, (await VoteAsync(client, code, "voter-aaaaaaaaaaaa", "nereye:walter")).GetProperty("totalVotes").GetInt32());
        Assert.Equal(2, (await VoteAsync(client, code, "voter-bbbbbbbbbbbb", "nereye:walter")).GetProperty("totalVotes").GetInt32());
        Assert.Equal(3, (await VoteAsync(client, code, "voter-cccccccccccc", "nereye:petra")).GetProperty("totalVotes").GetInt32());
        // Oy değiştirme: aynı kişi tek oy.
        var after = await VoteAsync(client, code, "voter-cccccccccccc", "nereye:kronotrop");

        Assert.Equal(3, after.GetProperty("totalVotes").GetInt32());
        Assert.Equal("nereye:kronotrop", after.GetProperty("myVote").GetString());
        Assert.Equal("nereye:walter", after.GetProperty("winnerPlaceId").GetString());
        var votes = after.GetProperty("options").EnumerateArray().ToDictionary(o => o.GetProperty("placeId").GetString()!, o => o.GetProperty("votes").GetInt32());
        Assert.Equal(0, votes["nereye:petra"]);
        Assert.Equal(2, votes["nereye:walter"]);

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/polls/{code}");
        req.Headers.Add("X-Voter", "voter-aaaaaaaaaaaa");
        var mine = JsonSerializer.Deserialize<JsonElement>(await (await client.SendAsync(req)).Content.ReadAsStringAsync(), Json);
        Assert.Equal("nereye:walter", mine.GetProperty("myVote").GetString());
    }

    [Fact]
    public async Task Invalid_polls_and_votes_are_rejected()
    {
        using var client = _factory.CreateClient();
        var one = await client.PostAsJsonAsync("/api/polls", new { title = "x", options = new[] { Option("nereye:petra", "Petra") } });
        Assert.Equal(HttpStatusCode.BadRequest, one.StatusCode);
        var dup = await client.PostAsJsonAsync("/api/polls", new { title = "x", options = new[] { Option("nereye:petra", "Petra"), Option("nereye:petra", "Petra") } });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);

        var poll = await CreateAsync(client, Option("nereye:petra", "Petra"), Option("nereye:walter", "Walter's"));
        var code = poll.GetProperty("code").GetString()!;
        var wrong = await client.PostAsJsonAsync($"/api/polls/{code}/votes", new { voterKey = "voter-dddddddddddd", placeId = "nereye:opera" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var badKey = await client.PostAsJsonAsync($"/api/polls/{code}/votes", new { voterKey = "x", placeId = "nereye:petra" });
        Assert.Equal(HttpStatusCode.BadRequest, badKey.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/polls/zzzzzzzz")).StatusCode);
    }

    [Fact]
    public async Task Poll_closes_after_a_day()
    {
        using var client = _factory.CreateClient();
        var poll = await CreateAsync(client, Option("nereye:petra", "Petra"), Option("nereye:walter", "Walter's"));
        var code = poll.GetProperty("code").GetString()!;
        _factory.Clock.Advance(TimeSpan.FromHours(25));
        try
        {
            var late = await client.PostAsJsonAsync($"/api/polls/{code}/votes", new { voterKey = "voter-eeeeeeeeeeee", placeId = "nereye:petra" });
            Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
            var got = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/polls/{code}"), Json);
            Assert.True(got.GetProperty("expired").GetBoolean());
        }
        finally
        {
            _factory.Clock.Advance(TimeSpan.FromHours(-25));
        }
    }

    [Fact]
    public async Task Web_page_lists_options_html_encoded()
    {
        using var client = _factory.CreateClient();
        var poll = await CreateAsync(client, Option("nereye:petra", "<script>alert(1)</script>"), Option("nereye:walter", "Walter's"));
        var code = poll.GetProperty("code").GetString()!;
        var res = await client.GetAsync($"/k/{code}");
        res.EnsureSuccessStatusCode();
        Assert.Equal("text/html", res.Content.Headers.ContentType?.MediaType);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Bu akşam kahve", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains($"data-code=\"{code}\"", html);
        Assert.Contains("'/votes'", html);

        var missing = await client.GetAsync("/k/zzzzzzzz");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
