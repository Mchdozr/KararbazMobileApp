# Nereye

Niyet bazlı mekan karar motoru. Kullanıcı "kahve / yemek / eğlence…" seçer, konumuna göre kısa bir öneri listesi alır.

## Yapı

| Yol | Ne |
| --- | --- |
| `apps/nereye-go` | **Canlı istemci.** Expo SDK 58, expo-router, TypeScript. Kendi `CLAUDE.md`/`AGENTS.md` dosyası var. |
| `apps/mobile` | Flutter arşivi. Dokunma. |
| `src/Nereye.Api` | ASP.NET Core 10 Web API (JWT, hız sınırı, statik medya). |
| `src/Nereye.Application` | Arayüzler ve DTO'lar. |
| `src/Nereye.Domain` | Saf kurallar (puanlama, geo, doğrulama). Bağımlılık yok. |
| `src/Nereye.Infrastructure` | EF Core (Postgres), Identity, OSM/Overpass/Nominatim/OSRM istemcileri. |
| `src/Nereye.Admin` | Blazor yönetim paneli. |
| `tests/` | xUnit: `Nereye.Domain.Tests` (saf), `Nereye.Api.Tests` (WebApplicationFactory, bellek içi DB, sahte mekanlar). |

## Komutlar

```bash
dotnet test Nereye.slnx                      # tüm backend testleri
cd apps/nereye-go && npm test                # istemci birim testleri
cd apps/nereye-go && npm run typecheck       # tsc --noEmit
```

Arayüzü Postgres/Redis olmadan uçtan uca çalıştırmak (sahte mekan verisiyle):

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5079 \
  Database__UseInMemory=true Redis__UseMemory=true Places__UseFake=true \
  dotnet run --project src/Nereye.Api --no-launch-profile
cd apps/nereye-go && EXPO_PUBLIC_API_URL=http://localhost:5079 npx expo export -p web --output-dir /tmp/nereye-web
npx serve -s -l 8099 /tmp/nereye-web         # sonra Playwright ile http://localhost:8099
```

Web'de harita yer tutucudur (`components/Map.web.tsx`); diğer her ekran gerçek API ile çalışır.

## Kurallar

- **Dil:** Kullanıcıya dönük her metin Türkçe. Kod ve tanımlayıcılar İngilizce, yorumlar Türkçe.
- **Hatalar (API):** İş kuralı ihlali için `Nereye.Application.Common` içindeki `ValidationException` / `NotFoundException` / `ForbiddenException` fırlat; `ApiErrorMiddleware` bunları `{ error, requestId }` gövdesine çevirir. Controller'da try/catch yazma.
- **Hatalar (istemci):** `lib/api.ts` `ApiError` fırlatır; ekranda `friendlyError(e)` ile göster. Ham hata metnini kullanıcıya basma.
- **Doğrulama kuralları tek yerde:** `AccountRules`, `ReviewRules`, `GeoBounds` (Domain). İstemcideki `lib/validation.ts` bunların aynasıdır; birini değiştirirsen diğerini de değiştir.
- **Şema:** EF modelini değiştiren her iş migration ister (`src/Nereye.Infrastructure/Persistence/Migrations`).
- **İstemci durumu:** Küçük global depolar (`lib/filters.ts`, `lib/favorites.ts`, `lib/saved-places.ts`) — yeni bir durum kütüphanesi ekleme.
- **Depolama:** Doğrudan `expo-secure-store` çağırma; `lib/storage.ts` kullan (web'de localStorage'a düşer).
- **Tasarım:** Renk/yazı tipi yalnızca `lib/theme.ts` üzerinden. `colors.coral` tarihsel bir addır, marka yeşilidir; hata için `colors.danger`. Dokunma hedefi en az 44pt, her `Pressable` için `accessibilityRole` + `accessibilityLabel`.
- **Geri bildirim:** Başarı/başarısızlık için `toast`, yükleme için `Skeleton`, boş durum için `EmptyState`. `Alert` yalnızca yıkıcı eylem onayı için.
- **Expo:** Paket eklerken `node_modules/expo/bundledNativeModules.json` içindeki sürümü kullan (SDK 58). RN 0.88'de ref tipleri `TextInputInstance` / `ScrollViewInstance`.
- **Mekan verisi:** Katalog OpenStreetMap; Google Places (New) üstüne zengin içerik ekler (özet, yorum, olanak, galeri, puan, fiyat). Google yanıtı yalnızca `GooglePlaceJson` ile ayrıştırılır, OSM ile `PlaceMerge` ile birleştirilir. Google yoksa, yavaşsa ya da kotası dolduysa her şey OSM ile çalışmaya devam etmeli — yeni kod bu kuralı bozmasın.
- **Google kotası kıt kaynaktır:** Kota uç bazında günlüktür (Nearby, Text Search, Details, Photo). Liste isteğine Atmosphere alanı (`editorialSummary`, `reviews`, `serves*`) ekleme; bunlar yalnızca detayda ve 24 saat önbellekle istenir. Fotoğraf tek genişlikte (800) istenir.
- **Önbellek:** `IPlacesCache` iki katmanlıdır (bellek + Redis) ve tekildir; dönen nesneyi değiştirme. Anahtar biçimi değişirse `PlacesCacheKey` içindeki sürümü artır.
- **Güvenlik:** Sır commit'leme. Google anahtarı yalnızca `GOOGLE_PLACES_API_KEY` ortam değişkeni, `.env` (gitignore'da) ya da `dotnet user-secrets` ile verilir; istemciye hiç gitmez (fotoğraflar `/api/media/google` yönlendirmesinden geçer). Production `JWT_SIGNING_KEY` ve `ADMIN_PASSWORD` ortam değişkeni ister.

## Çalışma biçimi

- Sormadan ilerle; varsayımını sonuçta tek cümleyle belirt. Geri alınamaz/dışa dönük işlerde onay iste.
- Kütüphane API'si için önce context7'ye (MCP) bak; erişilemiyorsa `node_modules` içindeki tip tanımları sürüm için doğruluk kaynağıdır.
- Davranış değişikliği = önce test. Bitti demeden önce yukarıdaki üç komut yeşil olmalı ve arayüz değiştiyse Playwright ile ekran görüntüsü alınmalı.
- Genel (tüm projeler için) kurulum: `tools/claude-global/`.
