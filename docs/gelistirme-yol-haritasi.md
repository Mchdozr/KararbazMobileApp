# Geliştirme yol haritası

Bu belge uygulamanın uçtan uca taranmasıyla çıktı: istemci (Expo), API, öneri/önbellek katmanı, dış servisler
(Overpass, OSRM, Google). Önce bu turda **yapılanlar** (ölçümleriyle), sonra **önceliklendirilmiş iş listesi**.
Çaba tahminleri kaba: S = yarım gün, M = 1–3 gün, L = 1 hafta+.

## Bu turda yapılanlar

### Doğruluk: "şu an açık" artık doğru

- **Sorun:** OSM'den gelen "açık mı" bilgisi veri çekildiği anda hesaplanıp önbelleğe 3 gün yazılıyordu. Sabah
  "açık" diye kaydedilen mekan gece de "açık" görünüyordu. Hesap ayrıca sunucunun saat diliminde yapılıyordu
  (Docker'da UTC, İstanbul'un 3 saat gerisi).
- **Sorun:** Ayrıştırıcı şu dört yazımı tanımıyordu:
  - gece yarısını geçen aralıklar (`Fr-Sa 18:00-02:00`)
  - günsüz kurallar (`09:00-23:00`)
  - çoklu aralıklar (`09:00-12:00,13:00-18:00`)
  - `24:00`

  En çok gece mekanları etkileniyordu.
- **Çözüm (`src/Nereye.Domain/Places/OpeningSchedule.cs`):**
  - OSM söz dizimi ve Google'ın Türkçe/İngilizce gün satırları ("Pazartesi: 09:00–23:00", "24 saat açık",
    "Kapalı", "9:00 AM – 10:00 PM") tek modele çevriliyor.
  - Hesap İstanbul saatiyle yapılıyor.
  - "Açık mı" bilgisi önbellekten değil her istekte yeniden hesaplanıyor (`OpenNowResolver`).
  - 17 birim testi ve 1 API testi eklendi.
- **Yeni bilgi:** Kart ve mekan sayfasında "23:00'e kadar açık", "Yakında kapanıyor · 23:00", "09:00'da açılıyor",
  "Yarın 09:00'da açılıyor" yazıyor (`closesAt`, `opensAt`… alanları). Türkçe ek uyumu saatin okunuşuna göre
  kuruluyor: 22:00'ye, 06:00'ya, 04:00'te.

### Doğruluk: yürüme süresi

- **Sorun:** Kullanılan genel OSRM sunucusu yalnızca araç profilini taşıyor. "Yürüyerek" süresi araç süresiydi:
  634 m için 2 dk, yani saatte ~23 km.
- **Çözüm:** Yaya rotasında hız gerçekçi değilse süre mesafe ÷ 4,8 km/sa ile hesaplanıyor. Aynı rota artık
  8 dk gösteriyor.

### Hız

| Ölçüm | Önce | Sonra |
| --- | --- | --- |
| Rota isteği (tekrar) | ~130–940 ms, her seferinde dış servis | ~4–8 ms. Rotalar 12 sa önbellekte, başarısız sonuçlar 2 dk. |
| Overpass, birincil uç yavaşken | sıralı deneme, 12 sn + 8 sn + 8 sn'ye kadar bekleme | 3,5 sn sonra yedek uç paralel başlıyor, ilk dolu cevap kullanılıyor; birincil uç hata verirse beklemeden geçiliyor |
| Soğuk açılış (yayın) | ana sayfadan önce `/health` yoklaması (bir gidiş-dönüş; ağ yokken 4 sn) | yoklama yok: tek adres varken atlanıyor |
| Açılış görüntüsü | font yüklenirken yükleniyor çarkı | yerel açılış ekranı oturum hazır olana dek kalıyor (`expo-splash-screen`) |

### Yeni özellik: "Benim için seç"

- **Ne yapıyor:** Uygulamanın asıl vaadi olan "karar motoru" artık tek dokunuşta.
  - Ana sayfadaki düğme, listedeki mekanlardan şu an için en uygununu gerekçeleriyle seçiyor: "8 dk yürüme",
    "23:00'e kadar açık", "4,6 puan · 120 yorum".
  - Seçilen kartta "Yol tarifi", "Detaya bak" ve "Başka öner" düğmeleri var.
- **Seçim kuralları (`lib/decide.ts`, testli):**
  - Reklamlı kart hiç seçilmiyor.
  - Kapanmak üzere ya da kapalı mekan geri düşüyor.
  - Temel sıralama sunucudan geliyor.

### Küçük cilalar

- Akşam bandına erişilebilirlik etiketi eklendi.
- Demo verisinin saatleri tutarlı hale getirildi (`FakePlacesClient`):
  - kafe günlük
  - gece kulübü gece yarısını aşıyor
  - park 7/24
  - "kapalı" örnek mekanın saati yok

## Öncelikli iş listesi

### P1 — Hız ve güvenilirlik

| İş | Neden | Çaba |
| --- | --- | --- |
| **Önbellekte bayat veriyi sunup arkada tazeleme ve popüler semtleri önceden ısıtma** | Yeni bir semtte ilk liste Overpass'e bağlı (soğuk 15–22 sn). Popüler semt × niyet çiftleri arka plan işiyle süre dolmadan tazelenirse kullanıcı hiç beklemez. | M |
| **ETag/304** (öneri ve mekan detayı) | Mobil veride tekrar açılışta gövde inmez. | S |
| **İstek iptali** (`AbortController`) | Niyet ya da konum hızla değişince eski istekler ağda sürüyor; şu an yalnızca cevapları yok sayılıyor. | S |
| **Kendi OSRM'imiz (yaya profiliyle) ya da yaya destekli bir sağlayıcı** | Genel OSRM sunucusu araç-only ve adil kullanım sınırlı. Gerçek yaya rotası tek yönlü sokakları doğru çizer. | M |
| **Kendi Overpass'ımız ya da ticari POI kaynağı** | Genel Overpass uçları yoğunlukta 429 dönüyor; yayın trafiğinde tek nokta riski. | L |
| **Görsel boyutları** | Demo görselleri 480 KB (1400 px), kartta 164 pt gösteriliyor. Yerel medya için 400/800 px varyant ve `srcset` mantığı. Google fotoğrafları zaten 800 px. | S |

### P1 — Profesyonel işletme

| İş | Neden | Çaba |
| --- | --- | --- |
| **Hata ve çökme raporlama** (Sentry ya da benzeri, kişisel veri ayıklanmış) | Şu an sahada oluşan hata görünmüyor; yalnızca sunucu günlükleri var. | S |
| **Ölçümler** (OpenTelemetry: uç başına p95, Overpass/Google hata oranı, önbellek isabeti) | Yukarıdaki hız işlerinin etkisini ölçmek için. | M |
| **Onaylı, gizlilik dostu kullanım analitiği** | Huni: açılış → liste → mekan → yol tarifi. KVKK açık rıza akışı zaten var (`/api/me/consent`). | M |
| **Uçtan uca testler** (web için Playwright, yerel için Maestro) | Arayüz değişiklikleri şu an elle ve ekran görüntüsüyle doğrulanıyor. | M |

### P2 — Ürün

| Özellik | Fikir | Çaba |
| --- | --- | --- |
| **Arkadaşlarla karar ver** | "Benim için seç"in grup sürümü: 3–5 mekanlık kısa liste bağlantıyla paylaşılır, arkadaşlar oylar, kazanan yol tarifine düşer. | L |
| **Bu akşam etkinlikler** | Mekanlarda etkinlik verisi var (`events`); ana sayfada "bu akşam" bandı ve bildirim. | M |
| **Hava durumuna göre öneri** | Yağmurda kapalı mekan, güneşte "Açık hava" öne çıkar. | M |
| **Koleksiyonlar** ("Kahve rotam") ve paylaşılabilir listeler | Kaydedilenler tek liste; adlandırılmış listeler ve paylaşım. | M |
| **Bildirimler** (açık rızayla) | "Kaydettiğin mekan şu an açık ve 5 dk uzakta", "Favorindeki etkinlik bu akşam". | M |
| **Karanlık tema** | Tema belirteçleri hazır (`lib/theme.ts`); sistem temasını izleyen ikinci palet. | M |
| **İngilizce arayüz** | İstanbul'da turist kitlesi büyük; metinler tek yerde toplanırsa kolay. | L |
| **Ana ekran bileşeni** | "Yakındaki açık kahveciler" iOS/Android widget'ı. | L |
| **İşletme paneli istatistikleri** | Sahiplenilmiş mekana görüntülenme, kaydetme, yol tarifi sayıları; öne çıkarmanın ölçülebilir değeri. | M |

## Kontrol listesi (her iş için)

- Davranış değişikliği önce testle başlar.
- `dotnet test Nereye.slnx`, `npm test` ve `npm run typecheck` yeşil olmalı.
- Arayüz değiştiyse Playwright ekran görüntüsü alınır.
- Şema değişirse migration yazılır (`src/Nereye.Infrastructure/Persistence/Migrations`).
- Google kotası kıt kaynaktır: liste isteğine Atmosphere alanı eklenmez (bkz. `CLAUDE.md`).
