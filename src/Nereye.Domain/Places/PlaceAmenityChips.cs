namespace Nereye.Domain.Places;

public static class PlaceAmenityChips
{
    private static readonly Dictionary<string, string> CuisineTr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["coffee"] = "Kahve",
        ["coffee_shop"] = "Kahve",
        ["turkish"] = "Türk",
        ["kebab"] = "Kebap",
        ["pizza"] = "Pizza",
        ["italian"] = "İtalyan",
        ["burger"] = "Burger",
        ["seafood"] = "Balık",
        ["bakery"] = "Fırın",
        ["dessert"] = "Tatlı",
        ["asian"] = "Asya",
        ["japanese"] = "Japon",
        ["chinese"] = "Çin",
        ["mexican"] = "Meksika",
        ["vegetarian"] = "Vejetaryen",
        ["vegan"] = "Vegan",
        ["regional"] = "Yöresel",
        ["international"] = "Dünya mutfağı",
    };

    public static IReadOnlyList<string> From(
        string? cuisine,
        string? outdoorSeating,
        string? internetAccess,
        bool hasPhone,
        bool hasWebsite)
    {
        var chips = new List<string>();
        foreach (var part in Split(cuisine))
        {
            var label = CuisineTr.TryGetValue(part, out var tr) ? tr : Humanize(part);
            if (!string.IsNullOrWhiteSpace(label) && !chips.Contains(label, StringComparer.OrdinalIgnoreCase))
            {
                chips.Add(label);
            }
        }

        if (IsYes(outdoorSeating))
        {
            chips.Add("Açık oturma");
        }

        if (IsWifi(internetAccess))
        {
            chips.Add("Wi-Fi");
        }

        // Telefon ve web sitesi zaten eylem düğmesi olarak görünür; rozet olarak tekrarlamıyoruz.
        return chips;
    }

    public static IReadOnlyList<string> From(PlaceSnapshot place)
    {
        var chips = From(place.Cuisine, place.OutdoorSeating, place.InternetAccess, false, false).ToList();
        foreach (var feature in place.Features ?? [])
        {
            // OSM "Açık oturma" ile Google "Açık alan" aynı şey.
            var twin = feature == "Açık alan" && chips.Contains("Açık oturma");
            if (!twin && !chips.Contains(feature, StringComparer.OrdinalIgnoreCase))
            {
                chips.Add(feature);
            }
        }

        return chips;
    }

    private static IEnumerable<string> Split(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Humanize(string value) =>
        string.Join(' ', value.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));

    private static bool IsYes(string? raw) =>
        raw is not null && raw.Trim() is "yes" or "true" or "1" or "terrace" or "seasonal";

    private static bool IsWifi(string? raw) =>
        raw is not null && raw.Trim() is "yes" or "wlan" or "wifi" or "true" or "1";
}
