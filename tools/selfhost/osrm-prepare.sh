#!/usr/bin/env bash
# OSRM verisini tek profil için hazırlar (MLD). Kullanım: osrm-prepare.sh foot|car
# Her profil kendi klasörüne yazar: /data/foot/istanbul.osrm, /data/car/istanbul.osrm
set -euo pipefail
PROFILE="$1"
DIR="/data/$PROFILE"
if [ -s "$DIR/istanbul.osrm.mldgr" ] && [ "${FORCE:-0}" != "1" ]; then
  echo "Hazır: $DIR (yeniden hazırlamak için FORCE=1)"
  exit 0
fi
mkdir -p "$DIR"
cp /data/istanbul.osm.pbf "$DIR/istanbul.osm.pbf"
osrm-extract -p "/opt/$PROFILE.lua" "$DIR/istanbul.osm.pbf"
osrm-partition "$DIR/istanbul.osrm"
osrm-customize "$DIR/istanbul.osrm"
rm -f "$DIR/istanbul.osm.pbf"
echo "Hazır: $DIR"
