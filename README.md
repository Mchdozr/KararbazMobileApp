# Nereye

Karar motoru. Canlı istemci **Expo `apps/nereye-go`**. Flutter `apps/mobile` arşiv.

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
dotnet test Nereye.slnx
cd apps/nereye-go
npm test
npm run typecheck
```

## Claude Code

Proje ayarları `.claude/settings.json`, `.mcp.json` ve `CLAUDE.md` içinde. Aynı kurulumu makinedeki tüm projelere uygulamak için: `tools/claude-global/install.ps1` (Windows) veya `install.sh`.

## Postgres olmadan hızlı deneme

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5079 Database__UseInMemory=true Redis__UseMemory=true Places__UseFake=true dotnet run --project src/Nereye.Api --no-launch-profile
```

Bellek içi veritabanı ve örnek Kadıköy mekanlarıyla açılır. Expo istemcisi `--web` ile de çalışır (harita yer tutucu olur).

## Prod notları

- `JWT_SIGNING_KEY` (en az 32 karakter) zorunlu; varsayılan anahtarla Production açılmaz
- `ADMIN_PASSWORD` verilmezse Production'da admin hesabı oluşturulmaz
- Ters vekil arkasındaysa `Proxy__TrustForwardedHeaders=true` (hız sınırı gerçek IP'yi görsün)
- Hız sınırı istemci başına: arama/öneri 90/dk, giriş uçları 12/dk, yazma 30/dk; 5 hatalı girişte hesap 5 dk kilitlenir
- `apps/nereye-go/app/legal.tsx` içindeki KVKK metni ve iletişim adresi yayın öncesi hukukçuya gösterilmeli

- `Database:UseInMemory=false` + PostgreSQL
- Redis
- `Places:UseFake=false` (OSM/Nominatim; Google key yok)
- JWT signing key env
- OSM rate limit (`osm` policy)
- `timestamp` kolonları Npgsql legacy switch ile duruyor; timestamptz ayrı migration

## Docker (API + Admin)

```powershell
docker compose up --build
```
