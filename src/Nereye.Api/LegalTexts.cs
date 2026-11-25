namespace Nereye.Api;

/// <summary>
/// Gizlilik (KVKK aydınlatma) ve kullanım koşulları metninin tek kaynağı. Uygulama bunu <c>/api/legal</c>'dan okur,
/// web'de <c>/gizlilik</c> ve <c>/kosullar</c> sayfalarında sunulur. TASLAKTIR: hukuki onay insan işidir
/// (docs/yayin-insan-gorevleri.md). İletişim adresi yalnızca insanın verdiği <c>Legal:Contact</c> ayarından gelir.
/// </summary>
public static class LegalTexts
{
    public sealed record Section(string Title, string Body);

    public sealed record Document(string Title, string Lead, IReadOnlyList<Section> Sections);

    public const string NoContact = "İletişim adresi tanımlanmadı.";

    public const string DraftNote = "Bu metin taslaktır; hukuki inceleme tamamlanmadı.";

    public static string? Contact(IConfiguration config)
    {
        var value = config["Legal:Contact"]?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static Document Privacy(string? contact) => new(
        "Gizlilik ve KVKK",
        "Bu metin, 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında Nereye uygulamasının verilerini nasıl işlediğini özetler.",
        [
            new("Hangi verileri işliyoruz?",
                "Hesap bilgilerin (ad, e-posta, şifrenin özeti, isteğe bağlı profil fotoğrafı), seçtiğin niyet ve atmosfer tercihleri, " +
                "kaydettiğin mekanlar ve konumlar, yazdığın yorumlar, yüklediğin fotoğraflar, engellediğin kullanıcılar ve yaptığın şikayetler. " +
                "Konumunu yalnızca sen izin verdiğinde ve yakınındaki mekanları önermek için kullanırız."),
            new("Neden işliyoruz?",
                "Sana uygun mekan önerileri sunmak, hesabını ve oturumunu yönetmek, e-posta adresini doğrulamak ve şifre sıfırlamak, " +
                "yorum ve şikayetleri değerlendirmek, hizmeti güvenli tutmak için."),
            new("Konum verisi",
                "Konumun cihazında tutulur ve öneri isteğiyle birlikte sunucuya koordinat olarak gönderilir. Geçmiş konumlarını saklamayız; " +
                "yalnızca kendin kaydettiğin konumlar (ev, iş) hesabında durur. Konum yalnızca uygulama açıkken kullanılır. " +
                "İzni cihaz ayarlarından istediğin an kapatabilirsin; uygulama semt seçerek de çalışır."),
            new("Hata kayıtları",
                "Uygulama bir hatayla karşılaşırsa hatanın teknik kaydını (hata metni, ekran adı, uygulama sürümü, işletim sistemi türü) " +
                "sunucumuza gönderir. Kayıt hesabına bağlanmaz; e-posta, konum, telefon numarası ve oturum anahtarı gönderilmeden önce " +
                "cihazında ve sunucuda ayıklanır. Kayıtlar 30 gün sonra silinir ve yalnızca hataları düzeltmek için kullanılır."),
            new("Kullanım istatistikleri (isteğe bağlı)",
                "Yalnızca izin verirsen hangi ekranların açıldığını anonim olarak sayarız: uygulama açılışı, seçilen niyet, açılan mekan, " +
                "yol tarifi, kaydetme ve paylaşma. Olaylar kimliğine, cihazına ya da konumuna bağlanmaz; sunucuda yalnızca günlük toplam " +
                "sayılar tutulur. Mekan sahipleri kendi mekanlarının toplam görüntülenme ve yol tarifi sayısını görür. İzni Profil " +
                "ekranından istediğin an kapatabilirsin."),
            new("Bildirimler (isteğe bağlı)",
                "Profil ekranından açarsan, kaydettiğin mekanlarda o akşam bir etkinlik olduğunda başlamadan önce bildirim göndeririz " +
                "(günde en fazla 3). Bunun için cihazının bildirim belirteci hesabınla birlikte saklanır ve Expo'nun bildirim servisi " +
                "üzerinden iletilir. Konumun bu amaçla kullanılmaz. Bildirimi kapattığında ya da hesabını sildiğinde belirteç silinir."),
            new("Kimlerle paylaşıyoruz?",
                "Mekan ve adres bilgisi için sunucumuz OpenStreetMap tabanlı servislere (Overpass, Nominatim, OSRM) ve Google Places'e " +
                "arama metni ve koordinat iletir; bu isteklere kimliğin eklenmez. Android'de harita Google Haritalar ile gösterilir; " +
                "harita bileşeni cihazından doğrudan Google'a bağlanır. E-posta doğrulama, şifre sıfırlama ve hesap silme e-postaları " +
                "bir e-posta gönderim hizmeti üzerinden gönderilir; bu hizmete yalnızca e-posta adresin ve mektubun içeriği iletilir. " +
                "Verilerini reklam amacıyla üçüncü kişilere satmayız."),
            new("Saklama süresi",
                "Verilerin hesabın açık olduğu sürece saklanır. Hesabını sildiğinde ad, e-posta, şifre, profil fotoğrafı, kayıtlı mekan ve " +
                "konumlar, tercihler, yorum metinleri ve yorum fotoğrafları silinir; yorumların yıldız puanı kimliksiz kalır. " +
                "Onay ve şikayet kayıtları yasal yükümlülükler için kimliksiz tutulur."),
            new("Hakların",
                "KVKK madde 11 kapsamında verilerine erişme, düzeltme, silme ve işlemeye itiraz etme hakkın var. Hesabını uygulamada " +
                "Profil › Hesap ve güvenlik › Hesabımı sil adımından ya da uygulamayı kurmadan bu sunucudaki /hesap-sil sayfasından " +
                "kalıcı olarak silebilirsin."),
            new("İletişim", contact is null ? NoContact : $"Sorular ve başvurular için: {contact}")
        ]);

    public static Document Terms(string? contact) => new(
        "Kullanım koşulları",
        "Nereye'yi kullanarak aşağıdaki kuralları kabul etmiş olursun.",
        [
            new("Kimler kullanabilir?",
                "Hesap açmak için 18 yaşını doldurmuş olmalısın. Hesabının güvenliğinden ve hesabınla yapılanlardan sen sorumlusun."),
            new("Yorum ve fotoğraf kuralları",
                "Yorumların kendi deneyimine dayanmalı. Hakaret, nefret söylemi, taciz, tehdit, cinsel içerik, kişisel bilgi paylaşımı, " +
                "yasa dışı içerik, reklam ve spam yasaktır. Başkasına ait fotoğrafı izinsiz yükleme. Yorum yazmak ve fotoğraf yüklemek " +
                "için e-posta adresini doğrulaman gerekir."),
            new("Şikayet ve engelleme",
                "Uygunsuz bir yorumu, fotoğrafı ya da kullanıcıyı uygulamadan şikayet edebilir, bir kullanıcıyı engelleyerek yorumlarını " +
                "kendinden gizleyebilirsin. Şikayetler incelenir; kurala aykırı içerik kaldırılır, kuralı tekrar ihlal eden hesap kapatılabilir."),
            new("Mekan bilgisi",
                "Mekan bilgileri OpenStreetMap ve Google gibi kaynaklardan gelir ve güncel olmayabilir. Gitmeden önce açılış saatlerini " +
                "kontrol et. Öne çıkan (sponsorlu) kartlar ayrıca işaretlenir."),
            new("Hesabın sona ermesi",
                "Hesabını istediğin an silebilirsin. Bu koşulları ihlal eden hesaplar uyarısız kapatılabilir."),
            new("İletişim", contact is null ? NoContact : $"Sorular ve başvurular için: {contact}")
        ]);
}
