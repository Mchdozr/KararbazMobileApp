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
```

## Prod notları

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
