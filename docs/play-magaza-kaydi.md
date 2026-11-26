# Play mağaza kaydı taslağı

Metinler taslaktır; mağaza görselleri ve son metin insan onayından geçer. Adresler "insan kendi API hostunu yazar"
biçimindedir; alan adı uydurulmamıştır.

## Uygulama kimliği

| Alan | Değer | Kaynak |
| --- | --- | --- |
| Uygulama adı | Kararbaz | `apps/nereye-go/app.json` (`expo.name`) |
| Paket adı | `com.mchdozr.nereye` | `apps/nereye-go/app.json` (`android.package`) |
| Sürüm | 1.0.1 (versionCode 3) | `apps/nereye-go/app.json`; artırma insan işi (`docs/yayin-insan-gorevleri.md`) |
| Uygulama şeması | `nereyego://` (paylaşılan mekan bağlantısı `nereyego://venue/{id}`) | `apps/nereye-go/app.json` (`scheme`), `apps/nereye-go/lib/share-text.ts` |
| Kategori | Seyahat ve Yerel (öneri; insan seçer) | — |
| Hedef kitle | 18+; Aileler programı yok | `src/Nereye.Api/LegalTexts.cs` |
| Reklam içerir | Evet: "Öne çıkan" sponsorlu kartlar | `apps/nereye-go/components/VenueProductCard.tsx` |
| Uygulama içi satın alma | Yok | `apps/nereye-go/app/owner/[id].tsx` |
| Gizlilik politikası | `https://{API hostu}/gizlilik` — insan kendi API hostunu yazar | `src/Nereye.Api/Controllers/LegalController.cs` |
| Hesap silme | `https://{API hostu}/hesap-sil` — insan kendi API hostunu yazar | `src/Nereye.Api/Controllers/AccountDeletionController.cs` |
| İletişim e-postası | İnsan verir (Play zorunlu alanı); repoda tanımlı değil | `src/Nereye.Api/appsettings.json` (`Legal:Contact` boş) |

## Kısa açıklama (en fazla 80 karakter)

> Kahve, yemek, kültür ya da gece: konumuna göre kısa ve net mekan önerisi.

## Uzun açıklama (taslak)

> Kararbaz, "şimdi nereye gitsem?" sorusuna kısa bir liste ile cevap verir.
>
> Ne istediğini seç (kahve, yemek, eğlence, kültür, açık hava, gece), Kararbaz bulunduğun yere göre yakındaki
> mekanları sıralasın. Uzun listelerde kaybolmak yok: önce sana en uygun birkaç yer.
>
> • Niyete göre öneri: Kültür'de müze ve tiyatro, Yemek'te restoran; karışık liste yok.
> • Şu an açık olanlar, mesafe ve yürüme/araç süresi.
> • Mekan sayfasında fotoğraflar, çalışma saatleri, kullanıcı yorumları.
> • Beğendiğin yerleri kaydet, ev ve iş konumlarını ekle.
> • Konum izni vermeden de semt seçerek kullanabilirsin.
>
> Yorumlar topluluk kurallarına tabidir: uygunsuz içeriği şikayet edebilir, kullanıcıları engelleyebilirsin.
> Hesabını uygulamadan ya da web'den istediğin an silebilirsin.
>
> Mekan verisi © OpenStreetMap katkıcıları; harita ve ek mekan bilgileri Google.

Not: "Öne çıkan" kartlar ücretli tanıtımdır; açıklamada gizlenmez.

## İçerik derecelendirmesi anketi (IARC) için notlar

| Soru alanı | Cevap | Kaynak |
| --- | --- | --- |
| Kullanıcılar birbiriyle etkileşir mi? | Evet: herkese açık yorumlar ve fotoğraflar | `apps/nereye-go/app/review/[id].tsx` |
| Kullanıcı içeriği denetleniyor mu? | Şikayet, engelleme, yönetici gizleme; yazmak için e-posta doğrulaması | `src/Nereye.Infrastructure/Reviews/ReviewService.cs`, `src/Nereye.Api/VerifiedEmail.cs`, `src/Nereye.Admin` |
| Konum paylaşımı | Konum başka kullanıcılarla paylaşılmaz | `apps/nereye-go/lib/geo.ts` |
| Dijital satın alma | Yok | — |
| Alkol | Bar ve gece mekanları listelenir (bilgi amaçlı); satış yok | `src/Nereye.Domain/Places/IntentPlaceTypes.cs` (`Gece`, `Eglence`) |

## Mağaza görselleri

İnsan onaylar. Ekran görüntüleri gerçek uygulamadan alınmalı; sahte mekan verisiyle (`Places__UseFake=true`)
alınan görüntüler mağazada kullanılmamalı. Ayrık `origin/main` `b8863aa` commit'i "mağaza görselleri" içeriyor;
elle bakılacak (`docs/yayin-riskleri.md`).

## Test kanalı

Kapalı test: 12 gerçek kullanıcı, 14 gün (güncel şartı Play Console'da doğrula). Testçi listesi uydurulmaz; bu
dosya kimseyi testçi olarak yazmaz.
