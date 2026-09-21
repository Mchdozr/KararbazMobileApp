namespace Nereye.Domain.Places;

public static class PlaceBlurb
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nereye:petra"] = "Petra’da çekirdek kokusu kapıdan karşılar. Filtre kahve net, köşe masa sakin — Kadıköy’de bir saati kendine ayır.",
        ["nereye:fazil"] = "Fazıl Bey’de Türk kahvesi ağır ağır pişer. Fincanın telvesine bakıp biraz hayal kurasın gelir.",
        ["nereye:walter"] = "Walter’s tam bir kavurucu dükkânı: taze çekim, açık tezgâh, konuşmanın biraz kısık olduğu o güzel kafe hali.",
        ["nereye:montag"] = "Montag’ın ışığı yumuşak, masalar geniş. Bir V60 al, laptop’u aç; öğleden sonra kendiliğinden uzar.",
        ["nereye:federica"] = "Federica samimi ve yakın. Bir tost, bir flat white — Moda’ya kaymadan önce tam durak.",
        ["nereye:goygoy"] = "Goygoy’un adı gibi rahat. Moda tarafında köpüklü bir cappuccino, ayakların dinlensin.",
        ["nereye:opera"] = "Opera Kahve geceye yakın durur. Gündüz kapalıysa üzülme; akşam ışığı başka güzel.",
        ["nereye:mandabatmaz"] = "Mandabatmaz’da kahve koyu, mekan küçük, kalabalık içten. Beyoğlu’nda bir yudum gelenek.",
        ["nereye:kronotrop"] = "Kronotrop Cihangir’de çekirdek ciddi iş. Tatlı bir ekşi, temiz bir bitiş — kahveyi sevenlere.",
        ["nereye:ciya"] = "Çiya’da tarhana, içli köfte, ev yemeği gibi ağırlar. Kadıköy’ün sofra hali, karnın bir kere doysun.",
        ["nereye:karakoy"] = "Karaköy Lokantası klasik, masalar düzenli, yemekler tanıdık. Liman tarafında içini ısıtır.",
        ["nereye:sureyya"] = "Süreyya’nın salonu eski İstanbul gibi durur. Bir bilet, biraz ürperme, çıkınca çay.",
        ["nereye:zorlu"] = "Zorlu PSM’de sahne büyük, fuaye kalabalık. Konser öncesi bir tur, sonra koltuğa.",
        ["nereye:modern"] = "İstanbul Modern’de denizle müze iç içe. Bir odaya gir, bir pencereye otur, şehri izle.",
        ["nereye:mini"] = "MiniMüzikhol gece ısınır. Kadıköy’ün küçük sahnesi, yüksek ses, terli mutluluk.",
        ["nereye:moda"] = "Moda Sahil’de rüzgâr tuzlu, çimen yumuşak. Kahveni al, denize bak, konuşmayı biraz bırak."
    };

    public static string Compose(PlaceSnapshot place, double distanceMeters, IReadOnlyList<VibeTag>? vibes = null)
    {
        if (Known.TryGetValue(place.PlaceId, out var known))
        {
            return known;
        }

        var where = Neighborhood(place.Address);
        var walk = Walk(distanceMeters);
        var open = place.OpenNow switch
        {
            true => "Şu an açık; içeri süzül.",
            false => "Şimdi kapalı olabilir; yine de yolunu öğren, sonra uğra.",
            _ => "Kapıyı çalmadan bak: belki tam senin saatin."
        };
        var vibe = VibeLine(vibes);
        var templates = Templates(place);
        var i = StableIndex(place.PlaceId, templates.Length);
        return string.Format(templates[i], place.Name, where, walk, open, vibe).Trim();
    }

    private static string[] Templates(PlaceSnapshot place)
    {
        if (place.Types.Any(t => t is "cafe" or "coffee_shop"))
        {
            return
            [
                "{0}, {1} — {2}. Filtre kahve taze, köşe masa sakin. {3} {4}",
                "{0} tam {2}. Çekirdek kokusu kapıdan gelir; {1} bir mola, kalbin yavaşlar. {3}",
                "{1} {0}. Köpük, fincan, kısa bir sessizlik. {2}. {4}",
                "{0}’da kahve ağır ağır konuşur. {2}; içeri gir, dünyayı biraz beklet. {3}"
            ];
        }

        if (place.Types.Any(t => t is "restaurant" or "meal_takeaway" or "fast_food"))
        {
            return
            [
                "{0}, {1}. {2} — karnın gerçeği söyler, sofra sessizce kurulur. {3} {4}",
                "{0}’da yemek tanıdık gelir. {2} ötede; bir kâse, bir nefes, sonra yol. {3}"
            ];
        }

        if (place.Types.Any(t => t is "park" or "tourist_attraction"))
        {
            return
            [
                "{0}, {1}. {2}. Çimen, rüzgâr, cep telefonunu biraz unut. {3}",
                "{0} açık havada durur. {2} yürüyüş; otur, bak, konuşmayı yavaşlat. {4}"
            ];
        }

        return
        [
            "{0}, {1}. {2}. İçeride bir köşe kap, bugünü biraz yumuşat. {3} {4}",
            "{0} tam {2}. {1} civarı; kapıdan içeri, kalabalıktan bir adım uzak. {3}"
        ];
    }

    private static string Neighborhood(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return "mahallende";
        }

        var part = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];
        return string.IsNullOrWhiteSpace(part) ? "mahallende" : $"{part} civarında";
    }

    private static string Walk(double meters)
    {
        if (meters <= 0)
        {
            return "yakınında";
        }

        if (meters < 120)
        {
            return "iki adım ötede";
        }

        if (meters < 450)
        {
            return "üç dakikalık yürüyüş";
        }

        if (meters < 1000)
        {
            return "kısa bir yürüyüş";
        }

        return $"{meters / 1000.0:0.0} km kadar";
    }

    private static string VibeLine(IReadOnlyList<VibeTag>? vibes)
    {
        if (vibes is null || vibes.Count == 0)
        {
            return string.Empty;
        }

        if (vibes.Contains(VibeTag.Laptop))
        {
            return "Laptop’a da yer var.";
        }

        if (vibes.Contains(VibeTag.Sessiz))
        {
            return "Sesi kısık, içi huzurlu.";
        }

        if (vibes.Contains(VibeTag.Romantik))
        {
            return "İki kişilik bir köşe gibi duruyor.";
        }

        if (vibes.Contains(VibeTag.Manzarali))
        {
            return "Cam kenarı ayrı güzel.";
        }

        return string.Empty;
    }

    private static int StableIndex(string id, int count)
    {
        var n = 17;
        foreach (var c in id)
        {
            n = (n * 31) + c;
        }

        return Math.Abs(n) % count;
    }
}
