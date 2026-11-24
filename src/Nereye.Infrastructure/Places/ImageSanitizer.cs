using Nereye.Application.Common;
using SkiaSharp;

namespace Nereye.Infrastructure.Places;

/// <summary>
/// Kullanıcı görselini yeniden kodlar: uzun kenar en fazla <c>maxSide</c> piksel, EXIF yönü uygulanmış,
/// tüm üst veri (EXIF, GPS, XMP) atılmış. Çözülemeyen ya da aşırı büyük (piksel bombası) dosya reddedilir.
/// SkiaSharp MIT lisanslıdır.
/// </summary>
public static class ImageSanitizer
{
    public const int UploadMaxSide = 1600;
    public const int AvatarMaxSide = 512;

    /// <summary>Çözülmeden önce reddedilen piksel sayısı (≈ 50 MP).</summary>
    public const long MaxSourcePixels = 50_000_000;

    private const int Quality = 85;

    public static (byte[] Bytes, string Extension) Normalize(byte[] input, int maxSide) =>
        Encode(input, (w, h) => Math.Min(1.0, (double)maxSide / Math.Max(w, h)), Quality);

    /// <summary>
    /// Görüntü genişliği en fazla <paramref name="width"/> olacak şekilde küçültür (EXIF yönü uygulanmış genişlik).
    /// Zaten dar olan görsel için null döner: özgün dosya sunulur.
    /// </summary>
    public static (byte[] Bytes, string Extension)? ResizeToWidth(byte[] input, int width)
    {
        var smaller = false;
        var result = Encode(input, (w, _) =>
        {
            smaller = w > width;
            return Math.Min(1.0, (double)width / w);
        }, VariantQuality, orientedScale: true);
        return smaller ? result : null;
    }

    /// <summary>Küçük sürümlerde gözle ayırt edilmeyen, dosyayı belirgin küçülten kalite.</summary>
    private const int VariantQuality = 80;

    /// <param name="scaleFor">Kaynak (ya da orientedScale ise yönü uygulanmış) genişlik ve yükseklikten ölçek.</param>
    private static (byte[] Bytes, string Extension) Encode(
        byte[] input,
        Func<int, int, double> scaleFor,
        int quality,
        bool orientedScale = false)
    {
        using var data = SKData.CreateCopy(input);
        using var codec = SKCodec.Create(data) ?? throw new ValidationException("Fotoğraf okunamadı.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxSourcePixels)
        {
            throw new ValidationException("Fotoğraf çok büyük.");
        }

        var format = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => SKEncodedImageFormat.Png,
            SKEncodedImageFormat.Webp => SKEncodedImageFormat.Webp,
            SKEncodedImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            _ => throw new ValidationException("Sadece jpg, png veya webp.")
        };

        var decodeInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888,
            format == SKEncodedImageFormat.Jpeg ? SKAlphaType.Opaque : SKAlphaType.Premul);
        using var source = SKBitmap.Decode(codec, decodeInfo) ?? throw new ValidationException("Fotoğraf okunamadı.");

        var turned = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var scale = orientedScale && turned ? scaleFor(info.Height, info.Width) : scaleFor(info.Width, info.Height);
        var w = Math.Max(1, (int)Math.Round(info.Width * scale));
        var h = Math.Max(1, (int)Math.Round(info.Height * scale));
        using var sized = scale < 1.0
            ? source.Resize(new SKImageInfo(w, h, decodeInfo.ColorType, decodeInfo.AlphaType), new SKSamplingOptions(SKCubicResampler.Mitchell))
              ?? throw new ValidationException("Fotoğraf küçültülemedi.")
            : source.Copy();

        var origin = codec.EncodedOrigin;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var outW = swap ? h : w;
        var outH = swap ? w : h;

        using var oriented = new SKBitmap(new SKImageInfo(outW, outH, decodeInfo.ColorType, decodeInfo.AlphaType));
        using (var canvas = new SKCanvas(oriented))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(Orientation(origin, outW, outH));
            using var sizedImage = SKImage.FromBitmap(sized);
            canvas.DrawImage(sizedImage, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }

        using var image = SKImage.FromBitmap(oriented);
        using var encoded = image.Encode(format, quality) ?? throw new ValidationException("Fotoğraf kaydedilemedi.");
        var ext = format switch
        {
            SKEncodedImageFormat.Png => ".png",
            SKEncodedImageFormat.Webp => ".webp",
            _ => ".jpg"
        };
        return (encoded.ToArray(), ext);
    }

    /// <summary>EXIF yönünü uygulayan dönüşüm: (x, y) kaynak noktası → çıktı noktası. W, H çıktı boyutudur.</summary>
    private static SKMatrix Orientation(SKEncodedOrigin origin, int W, int H) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, W, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, W, 0, -1, H, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, H, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, W, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, W, -1, 0, H, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, H, 0, 0, 1),
        _ => SKMatrix.Identity
    };
}
