using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Nereye.Domain.Polls;

/// <summary>
/// "Arkadaşlarla karar ver" kuralları: 2–5 mekanlık kısa liste, 24 saat açık, kişi başı tek oy (değiştirilebilir).
/// Oy verenin anahtarı yalnızca özet olarak saklanır; ad, e-posta ya da hesap bağlantısı yok.
/// </summary>
public static partial class PollRules
{
    public const int MinOptions = 2;
    public const int MaxOptions = 5;
    public const int TitleMax = 60;
    public const int NameMax = 120;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    // Karışan karakterler yok (0/o, 1/l/i): sesli okunup elle yazılabilsin.
    private const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";

    public static string NewCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }

    public static bool IsCode(string? code) => code is { Length: 8 } && code.All(c => Alphabet.Contains(c));

    public static bool IsVoterKey(string? key) => key is not null && VoterKey().IsMatch(key);

    public static string HashVoter(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("nereye-poll|" + key)))[..32];

    public static bool OptionCountOk(int count) => count is >= MinOptions and <= MaxOptions;

    /// <summary>En çok oy; eşitlikte listede önce gelen. Hiç oy yoksa null.</summary>
    public static string? Winner(IEnumerable<(string PlaceId, int Position, int Votes)> options)
    {
        var best = options.Where(o => o.Votes > 0).OrderByDescending(o => o.Votes).ThenBy(o => o.Position).FirstOrDefault();
        return best.PlaceId;
    }

    /// <summary>Görsel adresi yalnızca sunucunun kendi yolu ya da https olabilir (web sayfasında img src).</summary>
    public static string? CleanPhotoUrl(string? url)
    {
        var u = url?.Trim();
        if (string.IsNullOrEmpty(u) || u.Length > 500)
        {
            return null;
        }

        if (u.StartsWith('/') && !u.StartsWith("//", StringComparison.Ordinal))
        {
            return u;
        }

        return Uri.TryCreate(u, UriKind.Absolute, out var abs) && abs.Scheme == Uri.UriSchemeHttps ? u : null;
    }

    public static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    private static partial Regex VoterKey();
}
