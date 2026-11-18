using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Nereye.Infrastructure.Places;
using SkiaSharp;

namespace Nereye.Api.Tests;

public static class TestImages
{
    /// <summary>w×h JPEG; içine yön (orientation) ve GPS benzeri metin taşıyan bir EXIF APP1 bölümü eklenir.</summary>
    public static byte[] JpegWithExif(int w, int h, ushort orientation)
    {
        using var bmp = new SKBitmap(w, h);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.SeaGreen);
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, w / 4f, h / 4f, red);
        }

        using var image = SKImage.FromBitmap(bmp);
        var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 80).ToArray();

        // TIFF (little-endian): IFD0'da iki girdi: Orientation (0x0112) ve ImageDescription (0x010E, GPS izi).
        var note = Encoding.ASCII.GetBytes("GPS 41.0082N 28.9784E\0");
        var tiff = new List<byte>();
        tiff.AddRange("II"u8.ToArray());
        tiff.AddRange(Le16(42));
        tiff.AddRange(Le32(8));
        tiff.AddRange(Le16(2));
        tiff.AddRange(Le16(0x010E)); tiff.AddRange(Le16(2)); tiff.AddRange(Le32((uint)note.Length)); tiff.AddRange(Le32(8 + 2 + 24 + 4));
        tiff.AddRange(Le16(0x0112)); tiff.AddRange(Le16(3)); tiff.AddRange(Le32(1)); tiff.AddRange(Le16(orientation)); tiff.AddRange(Le16(0));
        tiff.AddRange(Le32(0));
        tiff.AddRange(note);
        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var segment = new byte[4 + payload.Length];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(payload.Length + 2));
        payload.CopyTo(segment, 4);
        return jpeg[..2].Concat(segment).Concat(jpeg[2..]).ToArray();
    }

    private static byte[] Le16(ushort v) => BitConverter.GetBytes(v);
    private static byte[] Le32(uint v) => BitConverter.GetBytes(v);
}

public sealed class ImageSanitizerTests
{
    [Fact]
    public void Large_photo_is_scaled_rotated_and_stripped_of_exif()
    {
        var input = TestImages.JpegWithExif(3000, 2000, orientation: 6);
        Assert.Contains("GPS 41.0082N", Encoding.ASCII.GetString(input));
        using (var codec = SKCodec.Create(new MemoryStream(input)))
        {
            Assert.Equal(SKEncodedOrigin.RightTop, codec.EncodedOrigin);
        }

        var (bytes, ext) = ImageSanitizer.Normalize(input, ImageSanitizer.UploadMaxSide);

        Assert.Equal(".jpg", ext);
        var text = Encoding.ASCII.GetString(bytes);
        Assert.DoesNotContain("Exif", text);
        Assert.DoesNotContain("GPS", text);
        using var output = SKCodec.Create(new MemoryStream(bytes));
        Assert.Equal(SKEncodedOrigin.TopLeft, output.EncodedOrigin);
        // 90° döndürülmüş: yatay 3000×2000 → dikey, uzun kenar 1600.
        Assert.Equal(1067, output.Info.Width);
        Assert.Equal(1600, output.Info.Height);
        // Sol üstteki kırmızı köşe, saat yönünde dönünce sağ üste geçer.
        using var bmp = SKBitmap.Decode(bytes);
        Assert.True(bmp.GetPixel(bmp.Width - 20, 20).Red > 200);
        Assert.True(bmp.GetPixel(20, 20).Red < 100);
    }

    [Fact]
    public void Small_photo_keeps_its_size_and_garbage_is_rejected()
    {
        var (bytes, _) = ImageSanitizer.Normalize(TestImages.JpegWithExif(640, 480, 1), ImageSanitizer.UploadMaxSide);
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        Assert.Equal(640, codec.Info.Width);
        var fake = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Throws<Nereye.Application.Common.ValidationException>(() => ImageSanitizer.Normalize(fake, 1600));
    }
}

[Collection("api-flow")]
public sealed class UploadSanitizingTests
{
    private readonly NereyeApiFactory _factory;

    public UploadSanitizingTests(NereyeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Uploaded_review_photo_is_stored_without_exif_and_goes_with_the_account()
    {
        using var client = _factory.CreateClient();
        var email = $"up{Guid.NewGuid():N}@nereye.test";
        var reg = JsonDocument.Parse(await (await client.PostAsJsonAsync("/api/auth/register",
            new { acceptTerms = true, email, password = "kahve1234", displayName = "Foto" })).Content.ReadAsStringAsync()).RootElement;
        await _factory.VerifyEmailAsync(email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", reg.GetProperty("accessToken").GetString());

        var input = TestImages.JpegWithExif(2400, 1800, 1);
        var res = await client.PostAsJsonAsync("/api/me/photos", new { base64 = Convert.ToBase64String(input), contentType = "image/jpeg" });
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, raw);
        var url = JsonDocument.Parse(raw).RootElement.GetProperty("url").GetString()!;
        (await client.PostAsJsonAsync("/api/venues/nereye:mini/reviews", new { stars = 5, text = "Foto", photoUrls = new[] { url } })).EnsureSuccessStatusCode();

        var stored = await client.GetByteArrayAsync(url);
        Assert.DoesNotContain("GPS", Encoding.ASCII.GetString(stored));
        using (var codec = SKCodec.Create(new MemoryStream(stored)))
        {
            Assert.Equal(1600, Math.Max(codec.Info.Width, codec.Info.Height));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/auth/me")).StatusCode);
        using var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(url)).StatusCode);
    }
}
