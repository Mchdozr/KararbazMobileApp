#!/usr/bin/env bash
# OSM verisini indirir ve İstanbul kutusuna kırpar (OSRM hazırlığı küçük veriyle hızlı ve az bellekle biter).
# Değişkenler: PBF_URL (kaynak), BBOX (batı,güney,doğu,kuzey). Çıktı: /data/istanbul.osm.pbf
set -euo pipefail
OUT=/data/istanbul.osm.pbf
if [ -s "$OUT" ] && [ "${FORCE:-0}" != "1" ]; then
  echo "Hazır: $OUT (yeniden indirmek için FORCE=1)"
  exit 0
fi
apt-get update -qq
apt-get install -y -qq curl osmium-tool ca-certificates > /dev/null
mkdir -p /data
curl -fL --retry 3 -o /data/source.osm.pbf "$PBF_URL"
osmium extract --overwrite -b "$BBOX" -o "$OUT" /data/source.osm.pbf
rm -f /data/source.osm.pbf
ls -lh "$OUT"
