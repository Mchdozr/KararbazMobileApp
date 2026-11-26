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

## Kısa açıklama (en fazla 80 karakter; 79)

> Kararsız mısın? Kahve, yemek, eğlence: yakınındaki en iyi yeri Kararbaz seçsin.

## Tam açıklama (en fazla 4000 karakter; 2117)

> "Nereye gidelim?" sorusunun cevabı artık tek dokunuş uzağında.
>
> Kararbaz, canın ne çektiğini seçtiğin anda bulunduğun yere göre kısa ve net bir mekan listesi çıkarır. Uzun listelerde kaybolmak, dakikalarca harita kaydırmak yok: önce sana en uygun birkaç yer.
>
> NE İSTEDİĞİNİ SEÇ
> Kahve, yemek, eğlence, kültür, açık hava ya da gece. Her niyet kendi mekanlarını getirir: Kültür'de müze ve tiyatro, Yemek'te restoran; karışık liste yok.
>
> BENİM İÇİN SEÇ
> Kararsız kaldığında tek dokunuşla en uygun yeri seçer ve nedenini söyler: şu an açık mı, kaç dakika yürüme, ne zamana kadar açık. Beğenmezsen "Başka öner" de.
>
> ARKADAŞLARINLA KARAR VER
> Kısa listeyi bir bağlantıyla paylaş, herkes oyunu versin. Uygulaması olmayan arkadaşların da web sayfasından oy verebilir. Kazanan yer belli olunca yol tarifi bir dokunuş uzağında.
>
> ŞU AN AÇIK MI?
> Çalışma saatlerine göre "23:00'e kadar açık", "Yakında kapanıyor" gibi net bilgiler. Mesafe, yürüme ve araç süresi, yol tarifi.
>
> FİLTRELE
> Şu an açık olanlar, sessiz, laptop dostu, manzaralı, romantik, çocuklu ve daha fazlası. Mesafeyi sen belirle.
>
> HAVAYA GÖRE ÖNERİ
> Yağmurda açık hava mekanlarını geriye, kapalı mekanları öne alır; güzel havada dış masası olan yerleri öne çıkarır.
>
> BU AKŞAM
> Yakınındaki mekanlarda bu akşam olan etkinlikleri ana sayfada gör.
>
> LİSTELERİN
> "Kahve rotam", "Hafta sonu" gibi kendi listelerini oluştur, istersen bağlantıyla paylaş. Beğendiğin yerleri kaydet.
>
> MEKAN SAYFASI
> Fotoğraflar, çalışma saatleri, olanaklar, kullanıcı yorumları ve yol tarifi tek sayfada.
>
> DAHA FAZLASI
> • Gece modu
> • Türkçe ve İngilizce arayüz
> • Android ana ekran bileşeni: yakındaki açık mekanlar ana ekranında
> • Konum izni vermeden de semt seçerek kullanabilirsin
>
> GİZLİLİK
> Kullanım istatistikleri ve bildirimler yalnızca iznin olursa açılır. Hesabını uygulamadan ya da web'den istediğin an silebilirsin. Yorumlar topluluk kurallarına tabidir; uygunsuz içeriği şikayet edebilir, kullanıcıları engelleyebilirsin.
>
> "Öne çıkan" olarak işaretli kartlar ücretli tanıtımdır.
>
> Mekan verisi © OpenStreetMap katkıcıları; ek mekan bilgileri Google. Hava durumu verisi MET Norway (CC BY 4.0).

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

Telefon ekran görüntüleri (8 adet, 1080x1920, 9:16): `docs/tanitim/magaza/`.

- Gerçek veriyle alındı: OpenStreetMap mekanları (kendi Overpass'ımız, İstanbul kırpımı), gerçek rota (OSRM),
  gerçek hava (MET Norway). Google anahtarı olmadığı için puan ve fotoğraf yok; uydurma puan ya da yorum yok.
- Uygulamanın web derlemesinden (aynı React Native arayüzü) alındı; telefon çerçevesi ve başlıklar sonradan
  eklendi. Harita ekranı web'de yer tutucu olduğu için kullanılmadı.
- Zincir bir markanın (Starbucks) tescilli logosu görünmesin diye o kayıt çekimde listeden çıkarıldı.
- Demo hesapla alındı (oylama, "Kahve rotam" listesi). İnsan onaylar; yayından önce gerçek cihazdan alınan
  görüntülerle değiştirilmesi daha iyi olur.

## Test kanalı

Kapalı test: 12 gerçek kullanıcı, 14 gün (güncel şartı Play Console'da doğrula). Testçi listesi uydurulmaz; bu
dosya kimseyi testçi olarak yazmaz.
