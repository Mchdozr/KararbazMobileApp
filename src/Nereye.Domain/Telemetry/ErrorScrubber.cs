using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Nereye.Domain.Telemetry;

/// <summary>
/// Hata metninden kişisel veriyi ayıklar: e-posta, erişim anahtarı (JWT/Bearer), koordinat, telefon, IP ve
/// adreslerdeki sorgu dizesi (konum ve arama metni taşır). İstemcideki lib/scrub.ts bunun aynasıdır.
/// </summary>
public static partial class ErrorScrubber
{
    public static string Scrub(string? text, int maxLength = 4000)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var s = Email().Replace(text, "<e-posta>");
        s = Bearer().Replace(s, "Bearer <anahtar>");
        s = Jwt().Replace(s, "<anahtar>");
        s = Query().Replace(s, "$1?<sorgu>");
        s = Coordinate().Replace(s, "<konum>");
        s = Ip().Replace(s, "<ip>");
        s = Phone().Replace(s, "<numara>");
        return s.Length > maxLength ? s[..maxLength] : s;
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._\-]+", RegexOptions.IgnoreCase)]
    private static partial Regex Bearer();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+(\.[A-Za-z0-9_\-]+)?")]
    private static partial Regex Jwt();

    // Yol korunur, sorgu dizesi atılır: /api/recommendations?lat=..&lng=.. → /api/recommendations?<sorgu>
    [GeneratedRegex(@"((?:https?://[^\s?#""']+)|(?:/[A-Za-z0-9_\-./\[\]]+))\?[^\s""')]+")]
    private static partial Regex Query();

    // Ondalıklı iki sayı yan yana (enlem, boylam): 40.98853, 29.02504 · 40.98853,29.02504
    [GeneratedRegex(@"-?\d{1,3}\.\d{3,}\s*,\s*-?\d{1,3}\.\d{3,}")]
    private static partial Regex Coordinate();

    [GeneratedRegex(@"\b\d{1,3}(\.\d{1,3}){3}\b")]
    private static partial Regex Ip();

    [GeneratedRegex(@"\+?\d[\d\s\-]{8,}\d")]
    private static partial Regex Phone();
}

/// <summary>Aynı hatayı tek satırda toplamak için: sayılar ve satır/sütun konumları yok sayılır.</summary>
public static partial class ErrorFingerprint
{
    public static string Of(string source, string message, string? stack)
    {
        var frames = (stack ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(3);
        var text = source + "|" + Digits().Replace(message, "#") + "|" + string.Join("|", frames.Select(f => Digits().Replace(f, "#")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}

/// <summary>İstemci hata raporu sınırları (sunucu doğrular; istemci de aynı sınırlarla keser).</summary>
public static class ClientErrorRules
{
    public const int MaxBatch = 10;
    public const int MessageMax = 500;
    public const int StackMax = 4000;
    public const int ScreenMax = 80;
    public const int VersionMax = 40;
    public const int RetentionDays = 30;

    public static readonly string[] Platforms = ["ios", "android", "web"];

    public static string Platform(string? value) =>
        value is not null && Platforms.Contains(value.Trim().ToLowerInvariant()) ? value.Trim().ToLowerInvariant() : "unknown";

    public static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }
}
