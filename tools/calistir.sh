#!/usr/bin/env bash
# Nereye: Docker yığınını açar ve Expo'yu telefonda görülecek şekilde başlatır (macOS / Linux).
# Repo kökünde çalıştır:  bash tools/calistir.sh
# Ön koşul: Docker açık, Node.js 22+, telefon ve bilgisayar aynı Wi-Fi'de.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "1/4 Docker kontrol ediliyor..."
docker info >/dev/null 2>&1 || { echo "Docker çalışmıyor. Docker'ı açıp tekrar dene."; exit 1; }

echo "2/4 Docker yığını derleniyor ve açılıyor (ilk sefer birkaç dakika sürer)..."
docker compose up -d --build

echo "3/4 API bekleniyor (http://localhost:5079/health)..."
for _ in $(seq 1 60); do
  if curl -fsS -m 3 http://localhost:5079/health >/dev/null 2>&1; then ok=1; break; fi
  sleep 2
done
if [ "${ok:-0}" != 1 ]; then docker compose logs --tail 40 api; echo "API 2 dakikada açılmadı."; exit 1; fi

# Telefonun ulaşacağı adres: varsayılan rotanın kaynak IPv4 adresi.
ip="$(ipconfig getifaddr en0 2>/dev/null || true)"
[ -n "$ip" ] || ip="$(ip route get 1.1.1.1 2>/dev/null | awk '{for(i=1;i<=NF;i++) if($i=="src") print $(i+1)}' || true)"
[ -n "$ip" ] || ip="$(hostname -I 2>/dev/null | awk '{print $1}' || true)"
[ -n "$ip" ] || { echo "Yerel ağ IP adresi bulunamadı."; exit 1; }
echo "    Telefon API'ye şu adresten bağlanacak: http://$ip:5079"

echo "4/4 Expo başlatılıyor. Expo Go ile QR kodu okut."
cd apps/nereye-go
if [ ! -d node_modules ] || [ package-lock.json -nt node_modules ]; then npm install; fi
EXPO_PUBLIC_API_URL="http://$ip:5079" npx expo start --lan --clear
