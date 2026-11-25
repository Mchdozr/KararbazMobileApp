using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nereye.Api.Tests;

[Collection("api-flow")]
public sealed class CollectionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly NereyeApiFactory _factory;

    public CollectionTests(NereyeApiFactory factory) => _factory = factory;

    private async Task<HttpClient> UserAsync()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new
        {
            acceptTerms = true,
            email = $"liste{Guid.NewGuid():N}@nereye.test",
            password = "Passw0rd!",
            displayName = "Liste"
        });
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object Item(string id, string name) =>
        new { placeId = id, name, category = "Kahve dükkanı", photoUrl = "/media/venues/petra.jpg", latitude = 40.9885, longitude = 29.025 };

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage res)
    {
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, raw);
        return raw.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }

    [Fact]
    public async Task User_builds_a_named_list_and_manages_items()
    {
        using var client = await UserAsync();
        var created = await ReadAsync(await client.PostAsJsonAsync("/api/me/collections", new { name = "Kahve rotam" }));
        var id = created.GetProperty("id").GetGuid();

        await ReadAsync(await client.PostAsJsonAsync($"/api/me/collections/{id}/items", Item("nereye:petra", "Petra")));
        await ReadAsync(await client.PostAsJsonAsync($"/api/me/collections/{id}/items", Item("nereye:walter", "Walter's")));
        // Aynı mekan ikinci kez eklenmez (hata da vermez).
        await ReadAsync(await client.PostAsJsonAsync($"/api/me/collections/{id}/items", Item("nereye:petra", "Petra")));

        var list = await client.GetFromJsonAsync<JsonElement>("/api/me/collections", Json);
        var mine = Assert.Single(list.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);
        Assert.Equal(2, mine.GetProperty("count").GetInt32());
        Assert.Equal("Kahve rotam", mine.GetProperty("name").GetString());
        Assert.False(string.IsNullOrEmpty(mine.GetProperty("coverUrl").GetString()));

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/me/collections/{id}", Json);
        Assert.Equal(["nereye:walter", "nereye:petra"], detail.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("placeId").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/me/collections/{id}/items/nereye:petra")).StatusCode);
        await ReadAsync(await client.PutAsJsonAsync($"/api/me/collections/{id}", new { name = "Kadıköy kahveleri" }));
        detail = await client.GetFromJsonAsync<JsonElement>($"/api/me/collections/{id}", Json);
        Assert.Equal("Kadıköy kahveleri", detail.GetProperty("name").GetString());
        Assert.Single(detail.GetProperty("items").EnumerateArray());

        // Hangi listelerde olduğu (mekan sayfasındaki "Listeye ekle" için).
        var containing = await client.GetFromJsonAsync<JsonElement>("/api/me/collections?placeId=nereye:walter", Json);
        Assert.True(containing.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id).GetProperty("contains").GetBoolean());
    }

    [Fact]
    public async Task Duplicate_or_empty_names_are_rejected_and_lists_are_private()
    {
        using var owner = await UserAsync();
        var id = (await ReadAsync(await owner.PostAsJsonAsync("/api/me/collections", new { name = "Gece" }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/me/collections", new { name = "gece" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/me/collections", new { name = "  " })).StatusCode);

        using var stranger = await UserAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/me/collections/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/me/collections/{id}/items", Item("nereye:petra", "Petra"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/me/collections/{id}")).StatusCode);

        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/collections")).StatusCode);
    }

    [Fact]
    public async Task Shared_list_is_readable_by_link_until_sharing_stops()
    {
        using var client = await UserAsync();
        var id = (await ReadAsync(await client.PostAsJsonAsync("/api/me/collections", new { name = "<b>Hafta sonu</b>" }))).GetProperty("id").GetGuid();
        await ReadAsync(await client.PostAsJsonAsync($"/api/me/collections/{id}/items", Item("nereye:moda", "Moda Sahili")));

        var share = await ReadAsync(await client.PostAsync($"/api/me/collections/{id}/share", null));
        var code = share.GetProperty("code").GetString()!;
        Assert.Equal($"nereyego://list/{code}", share.GetProperty("appUrl").GetString());
        Assert.EndsWith($"/l/{code}", share.GetProperty("webUrl").GetString());

        using var anyone = _factory.CreateClient();
        var pub = await anyone.GetFromJsonAsync<JsonElement>($"/api/lists/{code}", Json);
        Assert.Equal("<b>Hafta sonu</b>", pub.GetProperty("name").GetString());
        Assert.Equal("nereye:moda", pub.GetProperty("items")[0].GetProperty("placeId").GetString());
        Assert.False(pub.TryGetProperty("userId", out _));

        var html = await anyone.GetStringAsync($"/l/{code}");
        Assert.Contains("&lt;b&gt;Hafta sonu&lt;/b&gt;", html);
        Assert.Contains("Moda Sahili", html);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/me/collections/{id}/share")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/lists/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/l/{code}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_the_account_deletes_lists()
    {
        using var client = await UserAsync();
        var id = (await ReadAsync(await client.PostAsJsonAsync("/api/me/collections", new { name = "Silinecek" }))).GetProperty("id").GetGuid();
        await ReadAsync(await client.PostAsJsonAsync($"/api/me/collections/{id}/items", Item("nereye:petra", "Petra")));
        var code = (await ReadAsync(await client.PostAsync($"/api/me/collections/{id}/share", null))).GetProperty("code").GetString();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/auth/me")).StatusCode);
        using var anyone = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/lists/{code}")).StatusCode);
    }
}
