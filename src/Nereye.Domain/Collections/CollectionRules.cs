using System.Globalization;
using System.Text.RegularExpressions;
using Nereye.Domain.Polls;

namespace Nereye.Domain.Collections;

/// <summary>Adlandırılmış listeler ("Kahve rotam"): kullanıcı başına 20 liste, liste başına 100 mekan.</summary>
public static partial class CollectionRules
{
    public const int NameMax = 40;
    public const int MaxCollections = 20;
    public const int MaxItems = 100;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string? CleanName(string? name)
    {
        var v = Spaces().Replace(name ?? "", " ").Trim();
        return v.Length is 0 or > NameMax ? null : v;
    }

    /// <summary>Büyük/küçük harf Türkçe kurala göre: "İstanbul" = "istanbul", "IŞIK" = "ışık".</summary>
    public static bool SameName(string a, string b) =>
        string.Equals(a.Trim().ToLower(Turkish), b.Trim().ToLower(Turkish), StringComparison.Ordinal);

    public static bool CanCreate(int existing) => existing < MaxCollections;

    public static bool CanAdd(int existingItems) => existingItems < MaxItems;

    /// <summary>Paylaşım kodu oylama koduyla aynı alfabeden (karışan harf yok).</summary>
    public static string NewShareCode() => PollRules.NewCode();

    public static bool IsShareCode(string? code) => PollRules.IsCode(code);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
