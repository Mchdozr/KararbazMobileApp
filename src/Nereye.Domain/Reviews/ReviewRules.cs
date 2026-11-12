using System.Text.RegularExpressions;

namespace Nereye.Domain.Reviews;

public static partial class ReviewRules
{
    public const int MaxTextLength = 1000;
    public const int MaxTags = 5;
    public const int MaxPhotos = 4;
    public const int MaxReasonLength = 200;

    public static bool StarsValid(int stars) => stars is >= 1 and <= 5;

    /// <summary>Boş metni null yapar, fazlasını keser.</summary>
    public static string? CleanText(string? text, int max = MaxTextLength)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    /// <summary>Yalnızca bu API'nin ürettiği yükleme yolları kabul edilir; dış URL yorumlara giremez.</summary>
    public static bool IsOwnUpload(string? url) => url is not null && UploadPath().IsMatch(url);

    [GeneratedRegex(@"^/media/uploads/[0-9a-f]{32}\.(jpg|png|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex UploadPath();
}
