# Nereye

Karar motoru. Canlı istemci **Expo `apps/nereye-go`**. Flutter `apps/mobile` arşiv.

## Telefonda tek komutla çalıştır (Docker + Expo Go)

Docker Desktop açıkken, telefon ve bilgisayar aynı Wi-Fi'deyken repo kökünde:

```powershell
powershell -ExecutionPolicy Bypass -File tools\calistir.ps1
```

macOS / Linux: `bash tools/calistir.sh`. Betik Postgres, Redis, migrate, API ve yönetim panelini açar,
API'nin hazır olmasını bekler, bilgisayarın Wi-Fi IP'sini bulup uygulamayı ona yönlendirir ve Expo'yu
başlatır; çıkan QR kodu Expo Go ile (iPhone'da Kamera) okut. Telefon bağlanamazsa güvenlik duvarında
5079 ve 8081 portlarına izin ver. Google anahtarı için kökteki `.env` dosyasına `GOOGLE_PLACES_API_KEY=...`.

## Çalıştırma (Visual Studio / CLI)

1. Docker: `docker compose up postgres redis`
2. `src/Nereye.Api` — `http://localhost:5079` (Postgres + Redis + OSM)
3. `src/Nereye.Admin` — `http://localhost:5118` — `admin@nereye.app` / `Admin!12345` (aynı Postgres)
4. Expo Go (`apps/nereye-go`): telefon ve PC aynı Wi-Fi

```powershell
cd apps/nereye-go
npx expo start
```

Girişte API: `http://192.168.1.248:5079` (LAN IP değişirse uygulamada düzelt)

JWT: `JWT_SIGNING_KEY` env. CORS prod’da `Cors:Origins`. Health: `GET /health`.

Android emülatör: `EXPO_PUBLIC_API_URL=http://10.0.2.2:5079`

## Test

```powershell
dotnet test tests/Nereye.Domain.Tests
dotnet test tests/Nereye.Api.Tests
cd apps/nereye-go
npm ci
npm test
npm run typecheck
```

`PostgresMigrationTests` gerçek Postgres ister: `NEREYE_TEST_PG="Host=localhost;Port=5432;Username=postgres;Password=postgres"` verilmezse "atlandı" görünür. CI bu değişkeni bir Postgres servisiyle verir (`.github/workflows/ci.yml`).

## Claude Code

Proje ayarları `.claude/settings.json`, `.mcp.json` ve `CLAUDE.md` içinde. Aynı kurulumu makinedeki tüm projelere uygulamak için: `tools/claude-global/install.ps1` (Windows) veya `install.sh`.

## Postgres olmadan hızlı deneme

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5079 Database__UseInMemory=true Redis__UseMemory=true Places__UseFake=true dotnet run --project src/Nereye.Api --no-launch-profile
```

Bellek içi veritabanı ve örnek Kadıköy mekanlarıyla açılır. Expo istemcisi `--web` ile de çalışır (harita yer tutucu olur).

## Google Places anahtarı

Anahtar repoya yazılmaz. Üç yoldan biriyle ver:

```powershell
# Visual Studio / dotnet run
dotnet user-secrets set "Places:ApiKey" "ANAHTAR" --project src/Nereye.Api
```

```powershell
# Docker: kök dizinde .env dosyası (.env.example'dan kopyala)
GOOGLE_PLACES_API_KEY=ANAHTAR
```

Ya da `GOOGLE_PLACES_API_KEY` ortam değişkeni. Anahtar boşsa uygulama yalnızca OpenStreetMap verisiyle çalışır.

Google Cloud Console'da:
- **Places API (New)** etkin olmalı; anahtarı yalnızca bu API'ye ve mümkünse sunucunun IP'sine kısıtla.
- **Kotalar:** Nearby Search, Text Search, Place Details ve Place Photo için günlük sınırlar ayrı ayrıdır. Sınır doluysa o uç Pasifik gece yarısına kadar devre dışı kalır ve uygulama OSM ile devam eder (log: `Google Places 429 …`). Zengin içeriğin her mekanda görünmesi için bu sınırları kullanımına göre yükselt.

## Veritabanı şeması

Açılışta migration yalnızca geliştirmede çalışır. Yayında şema ayrı, tek seferlik bir adımla güncellenir; ayrıntı `docs/veritabani-guncelleme.md`.

```bash
dotnet ef database update --project src/Nereye.Infrastructure --startup-project src/Nereye.Api
# ya da yayımlanmış imajda:
dotnet Nereye.Api.dll migrate
```

Bekleyen migration varken API ve yönetim paneli açılmaz (hata mesajı hangi migration'ın beklediğini yazar).

## Prod notları

- `JWT_SIGNING_KEY` (en az 32 karakter) zorunlu; varsayılan anahtarla Production açılmaz
- `ADMIN_PASSWORD` verilmezse Production'da admin hesabı oluşturulmaz
- **Yönetim paneli (`src/Nereye.Admin`) herkese açık internete konmaz.** Ayrı bir host, VPN ya da IP kısıtı (ters vekil/güvenlik duvarı) insan tarafından kurulur; kod bunu yapmaz. Panelde 5 hatalı girişte hesap kilitlenir.
- Ters vekil arkasındaysa `Proxy__TrustForwardedHeaders=true` (hız sınırı gerçek IP'yi görsün)
- Hız sınırı istemci başına: arama/öneri 90/dk, giriş uçları 12/dk, yazma 30/dk; 5 hatalı girişte hesap 5 dk kilitlenir
- Gizlilik ve koşullar metni `src/Nereye.Api/LegalTexts.cs` içindedir (uygulama `/api/legal`'dan okur, web'de `/gizlilik`, `/kosullar`). Taslaktır; yayın öncesi hukukçu onayı gerekir. İletişim adresi `Legal:Contact` ayarı insan tarafından verilmezse "İletişim adresi tanımlanmadı" yazar.
- E-posta (doğrulama, şifre sıfırlama, hesap silme): `Email__Provider` (resend|postmark), `Email__ApiKey`, `Email__From` ortam değişkenleri ve `Api__PublicBaseUrl` (https). Biri eksikse bu akışlar 503 ile kapalı kalır.

- `Database:UseInMemory=false` + PostgreSQL
- Redis
- `Places:UseFake=false` (OSM/Nominatim; Google key yok)
- JWT signing key env
- OSM rate limit (`osm` policy)
- Zaman sütunları `timestamptz` (UTC); `TimestampTz` migration'ı eski değerleri UTC kabul ederek dönüştürür

## Docker (API + Admin)

```powershell
docker compose up --build
```
