# Nereye Tasarım Spec

Tarih: 2026-09-18  
Durum: onaylı ürün planının uygulama spec’i  
Yığın: Flutter (iOS/Android) + ASP.NET Core API + Blazor Admin + PostgreSQL + Redis

## 1. Problem

Dışarıdaki “nereye gidelim?” kararı uzun sürer. Nereye rehber değil karar motorudur: niyet × konum × an ile 5–8 mekan keser.

Kuzey yıldızı KPI: uygulama açılışı → yol tarifi veya kaydet (medyan < 45 sn).

## 2. Kapsam

### Faz 1 (MVP)

- E-posta kayıt / giriş, JWT + refresh, hesap silme
- Konum (veya manuel lat/lng) + niyet seçimi
- `GET /recommendations` → 5–8 kart (foto, mesafe, açık/kapalı, Nereye puanı)
- Harita + liste keşif (Places pin’leri Google Map attribution ile)
- Mekan detay: Google anlık alanlar + Nereye overlay
- Yorum (1–5 + etiket + opsiyonel foto URL), kaydet, şikayet
- Metin arama
- Blazor admin: şikayet kuyruğu, kullanıcı ban, vibe sözlüğü

### Faz 2

- Kullanıcı tercihleri (niyet + vibe + yarıçap)
- “Şu an açık” filtresi ve açık değilse skor cezası
- Vibe eşleşmesi skor bileşeni
- Kültür mekanları için etkinlik saatleri (`CultureEvent`)

### Faz 3

- Mekan sahiplenme (claim)
- İşletme foto/açıklama güncelleme, yorum yanıtı
- Etiketli öne çıkarma (skora girmez, max 2 slot)
- Ödeme soyutlaması (sandbox; iyzico/Stripe bağlanabilir)

### Dışarıda (YAGNI)

Bilet satışı, sohbet, dating, mikroservis, ML reco.

## 3. Niyet ve vibe

Ana niyetler: `kahve`, `yemek`, `eglence`, `kultur`, `acik_hava`, `gece`.

Kültür altı (filtre/etiket): `tiyatro`, `sinema`, `muze`, `canli_muzik`.

Vibe: `sessiz`, `laptop`, `manzarali`, `romantik`, `ucuz`, `premium`, `cocuk_dostu`, `gec_saat`, `tarihi`.

Google type eşlemesi:

| Niyet | Places types |
| --- | --- |
| kahve | cafe, coffee_shop |
| yemek | restaurant, meal_takeaway |
| eglence | bar, night_club, bowling_alley |
| kultur | performing_arts_theater, movie_theater, museum |
| acik_hava | park, tourist_attraction |
| gece | night_club, bar |

## 4. Places ToS

Kalıcı PostgreSQL’de yalnızca `google_place_id` (süresiz) ve Nereye overlay. Google name/address/hours/photos kaynak gerçeği değil; Redis TTL 15–60 dk. Lat/lng Places’ten gelirse en fazla 30 gün.

Foto: Google photo reference + attribution. Kullanıcı/işletme foto bizim blob.

Pre-fetch yok: cache yalnızca kullanıcı tetikli istek sonrası.

Detayda Google puanı attribution ile gösterilebilir; liste skoru Nereye Bayesian average kullanır (yoksa nötr 0.5).

## 5. Öneri skoru v1

```
score =
  0.35 * proximity
+ 0.20 * nereyeRating
+ 0.20 * intentMatch
+ 0.10 * isOpenNow
+ 0.10 * vibeMatch
+ 0.05 * recentSaves
- 0.15 * recentlyVisited
```

- `proximity`: 0–1500 m doğrusal 1→0, üzeri 0
- `nereyeRating`: Bayesian average, prior 3.5 / 8 oy
- `intentMatch`: primary type overlap 0 veya 1
- `isOpenNow`: açık 1, bilinmiyor 0.5, kapalı 0 (kultur/yemek kapalıysa ekstra 0.25 ceza skordan düşülür)
- `vibeMatch`: kullanıcı vibe ∩ mekan vibe / max(1, kullanıcı vibe sayısı)
- Boost skora **girmez**. Sponsor slotları listede ayrı, etiketli, max 2. Organik en az 5 kart korunur.

Kart sayısı: organik 5–8 + sponsor 0–2.

## 6. Ekranlar (Flutter)

1. Splash → oturum kontrol
2. Giriş / kayıt
3. Konum izni (red: manuel semt; API lat/lng)
4. Niyet onboarding (çoklu chip, en az 1)
5. Ana: niyet chip, 5–8 kart
6. Keşif: harita/liste, filtre sheet (mesafe, açık, min puan, vibe)
7. Detay: galeri, puanlar, saat, etiket, yol, kaydet, yorum
8. Yorum yaz
9. Ara
10. Kaydedilenler
11. Profil / tercihler / hesap sil
12. İşletme: claim, kampanya (Faz 3)

## 7. Veri modeli

- `Users`, `RefreshTokens`, `UserPreferences`, `ConsentLogs`
- `Venues` (google_place_id unique, claimed, owner, rating_avg, rating_count, primary_intent, geohash)
- `VenueTags`, `VenuePhotos` (bizim URL’ler)
- `Reviews`, `ReviewTags`, `ReviewPhotos`, `ReviewReplies`
- `Favorites`, `Reports`
- `CultureEvents` (venue, starts_at, ends_at, title)
- `VenueClaims`
- `PromotionCampaigns`, `PromotionImpressions`
- `Payments` (provider, external_id, status, amount)

Google alanları DTO’da `google` bloğu; overlay `nereye` bloğu.

## 8. API

Auth: `POST /api/auth/register|login|refresh|logout`, `DELETE /api/auth/me`

Tercih: `GET|PUT /api/me/preferences`

Reco: `GET /api/recommendations?lat&lng&intent&radiusMeters&openNow&vibes`

Venue: `GET /api/venues/{placeId}`, `GET /api/search?q&lat&lng`

Review/favorite/report: REST nested under venue

Kültür: `GET /api/venues/{placeId}/events`

Claim: `POST /api/venues/{placeId}/claims`

Kampanya: `POST /api/promotions`, `GET /api/promotions/mine`

Ödeme: `POST /api/promotions/{id}/checkout` → sandbox succeeded

Admin: `/api/admin/reports`, `/api/admin/users/{id}/ban`, `/api/admin/tags`

## 9. Mimari

Modular monolith: Domain (skor, kurallar) → Application (use case) → Infrastructure (EF, Redis, Places HTTP, sandbox payment) → Api / Admin.

Places anahtarı yalnızca sunucuda. Maps SDK anahtarı uygulama kısıtlı.

Geliştirme: `FakePlacesClient` (İstanbul tohumu) — `Places:UseFake=true`.

## 10. KVKK

Konum yalnızca öneri anında. Hesap silmede yorumlar “Silinmiş kullanıcı” olarak anonimleşir. Consent log. Google ToS/Privacy linkleri.

## 11. Admin

Blazor Server, cookie veya JWT ile `Admin` rolü. Şikayet çözümleme, ban, vibe sözlüğü seed/edit.

## 12. Test

Domain birim testleri (skor, Bayesian, slot, açık ceza, claim). API WebApplicationFactory + InMemory Places/Redis. Flutter widget testleri (SDK varsa).
