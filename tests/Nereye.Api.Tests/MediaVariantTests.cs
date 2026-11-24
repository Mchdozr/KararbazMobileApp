using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Nereye.Infrastructure.Places;
using SkiaSharp;

namespace Nereye.Api.Tests;

[Collection("api-flow")]
public sealed class MediaVariantTests
{
    private readonly NereyeApiFactory _factory;

    public MediaVariantTests(NereyeApiFactory factory) => _factory = factory;

    private static int WidthOf(byte[] bytes)
    {
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        return codec.Info.Width;
    }

    [Fact]
    public async Task Card_sized_variant_is_resized_and_much_smaller()
    {
        using var client = _factory.CreateClient();
        var original = await client.GetByteArrayAsync("/media/venues/petra.jpg");
        var res = await client.GetAsync("/media/venues/petra.jpg?w=400");
        res.EnsureSuccessStatusCode();
        var bytes = await res.Content.ReadAsByteArrayAsync();

        Assert.Equal("image/jpeg", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, WidthOf(bytes));
        Assert.True(bytes.Length * 4 < original.Length, $"{bytes.Length} / {original.Length}");
        Assert.Equal("public, max-age=86400", res.Headers.CacheControl?.ToString());
        Assert.NotNull(res.Headers.ETag);
    }

    [Fact]
    public async Task Width_snaps_up_and_large_requests_get_the_original()
    {
        using var client = _factory.CreateClient();
        var snapped = await client.GetByteArrayAsync("/media/venues/petra.jpg?w=500");
        Assert.Equal(800, WidthOf(snapped));

        var original = await client.GetByteArrayAsync("/media/venues/petra.jpg");
        var huge = await client.GetByteArrayAsync("/media/venues/petra.jpg?w=5000");
        Assert.Equal(original.Length, huge.Length);
    }

    [Fact]
    public async Task Variant_is_encoded_once_then_served_from_disk()
    {
        using var client = _factory.CreateClient();
        var store = _factory.Services.GetRequiredService<MediaVariantStore>();
        (await client.GetAsync("/media/venues/ciya.jpg?w=800")).EnsureSuccessStatusCode();
        var encodes = store.Encodes;
        (await client.GetAsync("/media/venues/ciya.jpg?w=800")).EnsureSuccessStatusCode();
        Assert.Equal(encodes, store.Encodes);
    }

    [Fact]
    public async Task Conditional_request_returns_304()
    {
        using var client = _factory.CreateClient();
        var first = await client.GetAsync("/media/venues/petra.jpg?w=400");
        using var again = new HttpRequestMessage(HttpMethod.Get, "/media/venues/petra.jpg?w=400");
        again.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await client.SendAsync(again);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Missing_file_and_other_folders_are_untouched()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/media/venues/yok.jpg?w=400")).StatusCode);
        var attribution = await client.GetAsync("/media/venues/ATTRIBUTION.md?w=400");
        Assert.NotEqual("image/jpeg", attribution.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Uploads_variant_keeps_no_cache_policy()
    {
        var uploads = Path.Combine(_factory.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().WebRootPath, "media", "uploads");
        Directory.CreateDirectory(uploads);
        var name = "variant-test-" + Guid.NewGuid().ToString("N") + ".jpg";
        await File.WriteAllBytesAsync(Path.Combine(uploads, name), Jpeg(1600, 1000));
        try
        {
            using var client = _factory.CreateClient();
            var res = await client.GetAsync($"/media/uploads/{name}?w=400");
            res.EnsureSuccessStatusCode();
            Assert.Equal(400, WidthOf(await res.Content.ReadAsByteArrayAsync()));
            Assert.Equal("public, no-cache", res.Headers.CacheControl?.ToString());
        }
        finally
        {
            File.Delete(Path.Combine(uploads, name));
        }
    }

    private static byte[] Jpeg(int w, int h)
    {
        using var bmp = new SKBitmap(w, h);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(new SKColor(30, 120, 90));
        }

        using var img = SKImage.FromBitmap(bmp);
        return img.Encode(SKEncodedImageFormat.Jpeg, 85).ToArray();
    }
}
