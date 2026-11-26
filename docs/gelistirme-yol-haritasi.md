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

## İkinci tur: iş listesinin tamamı yapıldı

Önceki iş listesindeki 19 iş sırayla yapıldı. Her satırda ayrıntı ve doğrulama durumu ilgili belgededir.

### P1 — Hız ve güvenilirlik

| İş | Durum |
| --- | --- |
| Bayat listeyi beklemeden sun, arkada tazele; popüler semtleri ısıt | Yapıldı (`CacheRefresher`, `PlacesWarmupService`). Isıtma `Places:Warmup:Enabled` ile açılır; yalnızca OSM'yi ısıtır, Google kotası harcanmaz. |
| ETag/304 | Yapıldı (`[ETag]`: öneri ve mekan detayı). |
| İstek iptali | Yapıldı (`AbortController`, niyet/konum değişince eski istek kesilir). |
| Kendi OSRM'imiz (yaya) ve Overpass'ımız | Uçlar yapılandırılabilir; kurulum `docker-compose.selfhost.yml`. Konteynerler bu ortamda ayağa kaldırılamadı. Bkz. [kendi-sunucular.md](kendi-sunucular.md). |
| Görsel boyutları | Yapıldı: 400/800 px sürümler. |

### P1 — Profesyonel işletme

| İş | Durum |
| --- | --- |
| Hata ve çökme raporlama | Yapıldı: kişisel veri ayıklanır, yönetim panelinde "Hatalar". |
| Ölçümler | Yapıldı: uç p95, dış servis hata oranı, önbellek isabeti. OTLP dışa aktarımı yazıldı ama gerçek bir toplayıcıyla denenmedi. Bkz. [olcumler.md](olcumler.md). |
| Onaylı kullanım analitiği | Yapıldı: rıza istemi, huni, yönetim panelinde "Kullanım". |
| Uçtan uca testler | Playwright (web) CI'da koşuyor. Maestro akışları yazıldı, cihaz olmadığı için çalıştırılmadı. Bkz. [uctan-uca-testler.md](uctan-uca-testler.md). |

### P2 — Ürün

| Özellik | Durum |
| --- | --- |
| Arkadaşlarla karar ver | Yapıldı: kısa liste bağlantıyla paylaşılır, uygulama ya da web sayfasından oylanır (`/k/{kod}`). |
| Bu akşam etkinlikler | Yapıldı: sahip etkinlik ekler, yakındakiler ana sayfada "Bu akşam" bandında. |
| Hava durumuna göre öneri | Yapıldı. Yayında kapalı: Open-Meteo'nun ücretsiz ucu ticari kullanıma kapalı, lisans kararı insanda. Bkz. [hava-durumu.md](hava-durumu.md). |
| Koleksiyonlar ve paylaşılabilir listeler | Yapıldı: adlandırılmış listeler, `/l/{kod}` paylaşım sayfası. |
| Bildirimler (açık rızayla) | Sunucu ve istemci yapıldı: kaydedilen mekandaki etkinlik hatırlatması. Gerçek cihazda denenmedi; Android'de FCM dosyası insan tarafından eklenir. Bkz. [bildirimler.md](bildirimler.md). "Kaydettiğin mekan açık ve 5 dk uzakta" bildirimi arka plan konumu gerektirdiği için yapılmadı. |
| Karanlık tema | Yapıldı: sistem temasını izler, Profil'de seçim, kontrast testli. |
| İngilizce arayüz | Ana akışlar çevrildi. Sunucudan gelen metinler (hata iletileri, hava notu, yasal metinler) Türkçe kalıyor. Bkz. [coklu-dil.md](coklu-dil.md). |
| Ana ekran bileşeni | Android yapıldı (config plugin). Derlenip cihazda denenmedi; iOS yapılmadı. Bkz. [ana-ekran-bileseni.md](ana-ekran-bileseni.md). |
| İşletme paneli istatistikleri | Yapıldı: sahip ekranında "Son 30 gün" (görüntülenme, yol tarifi, kaydetme, paylaşım, dönüşüm). |

### Sıradaki işler (insan ya da cihaz gerektirenler)

- İlk yerel Android derlemesinde: bileşen, bildirim izni/belirteci ve Maestro akışları.
- Open-Meteo ticari abonelik ya da başka sağlayıcı kararı.
- Kendi OSRM/Overpass konteynerlerinin gerçek sunucuda kurulumu.
- OTLP toplayıcısı seçimi ve bağlantısı.
- Sunucu metinlerinin İngilizcesi (`Accept-Language`).
- iOS bileşeni (WidgetKit + App Group).

## Kontrol listesi (her iş için)

- Davranış değişikliği önce testle başlar.
- `dotnet test Nereye.slnx`, `npm test` ve `npm run typecheck` yeşil olmalı.
- Arayüz değiştiyse Playwright ekran görüntüsü alınır.
- Şema değişirse migration yazılır (`src/Nereye.Infrastructure/Persistence/Migrations`).
- Google kotası kıt kaynaktır: liste isteğine Atmosphere alanı eklenmez (bkz. `CLAUDE.md`).
