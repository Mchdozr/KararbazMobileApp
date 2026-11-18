# Play Veri güvenliği formu için kaynak

Her satır, verinin kodda nerede toplandığını gösterir. Form Play Console'da insan tarafından doldurulur; Play'in
kategori adları ve "paylaşım" tanımı değişebilir, son karar formu dolduran kişinindir.

## Genel

| Soru | Cevap | Kaynak |
| --- | --- | --- |
| Uygulama kullanıcı verisi topluyor mu? | Evet | aşağıdaki tablo |
| Veriler aktarımda şifreleniyor mu? | Evet: production derlemesi yalnızca https API adresiyle derlenir, cleartext kapalı | `apps/nereye-go/app.config.ts`, `apps/nereye-go/lib/release-config.ts` |
| Kullanıcı verisinin silinmesini isteyebilir mi? | Evet: uygulamada ve uygulamasız web sayfasında | `apps/nereye-go/app/account.tsx`, `src/Nereye.Api/Controllers/AccountDeletionController.cs` (`/hesap-sil`) |
| Hesap silme adresi | İnsan kendi API hostunu yazar: `https://{API hostu}/hesap-sil` | `docs/yayin-insan-gorevleri.md` |
| Gizlilik politikası adresi | İnsan kendi API hostunu yazar: `https://{API hostu}/gizlilik` | `src/Nereye.Api/Controllers/LegalController.cs` |
| Reklam içeriyor mu? | **Evet** — "Öne çıkan" (sponsorlu) mekan kartları listede gösterilir. Reklam SDK'sı ya da reklam kimliği yok | `apps/nereye-go/components/VenueProductCard.tsx` (`kind === 'Sponsored'`), `src/Nereye.Domain/Entities/PromotionCampaign.cs` |
| Hedef kitle | 18+; Aileler programında değil | `src/Nereye.Api/LegalTexts.cs` (Kullanım koşulları) |
| Uygulama içi satın alma | Yok | `apps/nereye-go/app/owner/[id].tsx` (iletişim bağlantısı), `src/Nereye.Infrastructure/Payments/UnavailablePaymentGateway.cs` |

## Toplanan veriler

| Play kategorisi | Veri | Neden | Zorunlu mu | Nerede toplanıyor | Nerede saklanıyor |
| --- | --- | --- | --- | --- | --- |
| Kişisel bilgi › E-posta adresi | Hesap e-postası | Hesap, giriş, doğrulama ve şifre sıfırlama e-postaları | Hesap açan için evet; hesapsız keşif mümkün | `apps/nereye-go/app/auth.tsx`, `src/Nereye.Infrastructure/Auth/AuthService.cs` (`RegisterAsync`) | `AspNetUsers` (`src/Nereye.Infrastructure/Identity/AppUser.cs`) |
| Kişisel bilgi › Ad | Görünen ad | Yorumların yanında gösterilir | Evet (hesapta) | `apps/nereye-go/app/auth.tsx`, `apps/nereye-go/app/account.tsx` | `AppUser.DisplayName` |
| Kişisel bilgi › Kullanıcı kimlikleri | Hesap kimliği (GUID), oturum token'ının özeti | Oturum yönetimi | Evet (hesapta) | `src/Nereye.Infrastructure/Auth/AuthService.cs` (`IssueAsync`) | `RefreshTokens` (`src/Nereye.Domain/Entities/RefreshToken.cs`) |
| Fotoğraflar | Profil fotoğrafı | Profilde ve yorumlarda gösterilir | İsteğe bağlı | `apps/nereye-go/app/(tabs)/profile.tsx`, `src/Nereye.Api/Controllers/MeController.cs` (`avatar`) | `wwwroot/media/avatars` (512 px, EXIF'siz: `src/Nereye.Infrastructure/Places/ImageSanitizer.cs`) |
| Fotoğraflar | Yorum fotoğrafları; işletme sahibinin mekan fotoğrafları | Mekan sayfasında gösterilir | İsteğe bağlı | `apps/nereye-go/app/review/[id].tsx`, `apps/nereye-go/app/owner/[id].tsx`, `src/Nereye.Api/Controllers/MeController.cs` (`photos`), `VenuesController` (`{placeId}/photos`) | `wwwroot/media/uploads` (1600 px, EXIF ve GPS atılmış) |
| Konum › Kesin konum | Cihaz konumu (izinle, yalnızca uygulama açıkken) | Yakındaki mekan önerisi, mesafe ve rota | İsteğe bağlı: semt seçerek de çalışır | `apps/nereye-go/lib/geo.ts` (`requestForegroundPermissionsAsync`), `apps/nereye-go/lib/api.ts` (`recommend`, `route`) | Sunucuda saklanmaz; istek başına kullanılır |
| Konum › Kesin konum | Kayıtlı konumlar (ev, iş vb.) | Hızlı konum seçimi | İsteğe bağlı | `apps/nereye-go/app/places.tsx`, `src/Nereye.Api/Controllers/MeController.cs` (`places`) | `SavedPlaces` (`src/Nereye.Domain/Entities/SavedPlace.cs`) |
| Uygulama etkinliği › Uygulama içi arama geçmişi | Konum arama metni | Adres önerisi | İsteğe bağlı | `apps/nereye-go/lib/api.ts` (`geoSuggest`), `src/Nereye.Api/Controllers/GeoController.cs` | Sunucuda kullanıcıyla bağlı saklanmaz (anonim önbellek). Son aramalar yalnızca cihazda (`apps/nereye-go/lib/recent-searches.ts`) |
| Uygulama etkinliği › Kullanıcı tarafından oluşturulan diğer içerik | Yorum metni, yıldız, etiketler | Mekan sayfasında gösterilir | İsteğe bağlı | `apps/nereye-go/app/review/[id].tsx`, `src/Nereye.Infrastructure/Reviews/ReviewService.cs` | `Reviews`, `ReviewTags` |
| Uygulama etkinliği › Diğer işlemler | Kaydedilen mekanlar, niyet/atmosfer tercihleri | Kişiselleştirme | İsteğe bağlı | `src/Nereye.Api/Controllers/VenuesController.cs` (`favorite`), `MeController` (`preferences`) | `Favorites`, `UserPreferences` |
| Uygulama etkinliği › Diğer işlemler | Şikayetler ve engellenen kullanıcılar | İçerik denetimi (UGC) | İsteğe bağlı | `apps/nereye-go/app/venue/[id].tsx`, `src/Nereye.Infrastructure/Reviews/ReviewService.cs` (`ReportAsync`, `BlockAsync`) | `Reports`, `UserBlocks` |
| Uygulama etkinliği › Diğer işlemler | Öne çıkan kart gösterimi/tıklaması (hesap kimliğiyle) | Kampanya sayımı | Otomatik | `src/Nereye.Domain/Entities/PromotionImpression.cs` | `PromotionImpressions`; hesap silinince kimlik bağı kaldırılır |
| Uygulama etkinliği › Diğer işlemler | Onay kayıtları (koşullar, KVKK, pazarlama) | Yasal kayıt | Koşullar zorunlu | `src/Nereye.Infrastructure/Auth/AuthService.cs`, `MeController` (`consent`) | `ConsentLogs` |
| Mesajlar › Diğer | İşletme sahipliği başvuru mesajı | Sahiplik doğrulaması | İsteğe bağlı | `src/Nereye.Application/Claims/IClaimService.cs` (`ClaimRequest`) | `VenueClaims` |

**Toplanmayanlar:** reklam kimliği, kişi listesi, SMS/arama kaydı, mikrofon, kamera, sağlık, finansal bilgi,
cihaz kimliği, çökme/analiz SDK'sı (Sentry, Firebase vb. yok). IP adresi yalnızca hız sınırı için bellekte
kullanılır, veritabanına yazılmaz (`src/Nereye.Api/Program.cs`).

## Üçüncü taraflara aktarım

Play "hizmet sağlayıcıya kendi adınıza işleme" aktarımını paylaşım saymayabilir; formu dolduran kişi güncel
tanıma göre karar verir.

| Alıcı | Ne gider | Kimlik | Kaynak |
| --- | --- | --- | --- |
| Google Places (sunucudan) | Arama metni, koordinat | Gitmez | `src/Nereye.Infrastructure/Places/GooglePlacesClient.cs` |
| Google Maps SDK (cihazdan, Android) | Harita gösterimi için cihazın Google'a bağlantısı | Google SDK'sı kendi verisini toplar | `apps/nereye-go/components/Map.tsx` |
| OpenStreetMap servisleri: Overpass, Nominatim, OSRM (sunucudan) | Koordinat, arama metni | Gitmez | `src/Nereye.Infrastructure/Places/*` |
| E-posta sağlayıcısı (Resend/Postmark) | E-posta adresi ve mektup içeriği | E-posta | `src/Nereye.Infrastructure/Email/EmailSenders.cs` |

## Silme

Uygulamada: Profil › Hesap ve güvenlik › Hesabımı sil (`apps/nereye-go/app/account.tsx` → `DELETE /api/auth/me`).
Web'de: `/hesap-sil` (e-postaya gelen bağlantı + onay düğmesi). Silinen ve kalanların listesi sayfada yazar;
uygulama `src/Nereye.Infrastructure/Auth/AuthService.cs` → `DeleteMeAsync`.
