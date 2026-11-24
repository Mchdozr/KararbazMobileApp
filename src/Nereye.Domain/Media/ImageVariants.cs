namespace Nereye.Domain.Media;

/// <summary>
/// Yerel görsellerin (mekan ve kullanıcı yüklemesi) küçültülmüş sürümleri. İstenen genişlik bilinen bir
/// sürüme yukarı yuvarlanır; böylece disk önbelleği sınırlı sayıda dosya tutar.
/// </summary>
public static class ImageVariants
{
    public static readonly int[] Widths = [400, 800, 1200];

    private static readonly string[] Folders = ["/media/venues/", "/media/uploads/"];
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>Sunulacak sürüm genişliği; null = özgün dosya (geçersiz ya da en büyük sürümden büyük istek).</summary>
    public static int? Snap(int requested)
    {
        if (requested <= 0)
        {
            return null;
        }

        foreach (var width in Widths)
        {
            if (width >= requested)
            {
                return width;
            }
        }

        return null;
    }

    public static bool Supports(string path) =>
        !path.Contains("..", StringComparison.Ordinal)
        && Folders.Any(f => path.StartsWith(f, StringComparison.Ordinal))
        && Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}
