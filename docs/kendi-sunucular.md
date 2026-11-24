# Kendi rota ve mekan sunucularımız

Uygulama varsayılan olarak genel sunucuları kullanır:

- **Rota:** `router.project-osrm.org`. Yalnızca araç profili taşır. Yürüme süresi mesafeden hesaplanır.
- **Mekan:** `overpass-api.de` ile iki yedek Overpass ucu.

Genel sunucular adil kullanım sınırlıdır ve yoğun saatte 429 döner. Yayın trafiğinde kendi sunucularımız
kurulur:

- **OSRM (yaya):** gerçek yürüme rotası çıkarır. Tek yönlü sokak ve merdiven yürüyene engel olmaz.
- **OSRM (araç):** sürüş rotası.
- **Overpass:** İstanbul verisiyle yakındaki mekan sorguları. Genel uçlar yedek olarak listede kalır.

## Kurulum

```bash
docker compose -f docker-compose.yml -f docker-compose.selfhost.yml up -d
```

Sıra kendiliğinden işler:

1. `osm-download` Türkiye verisini Geofabrik'ten indirir ve İstanbul kutusuna kırpar (`osmium extract`).
2. `osrm-prepare-foot` ve `osrm-prepare-car` hazırlığı yapar (`osrm-extract`, `osrm-partition`, `osrm-customize`, MLD).
3. Ardından `osrm-foot` (5001) ve `osrm-car` (5002) başlar.
4. `overpass` ilk açılışta veritabanını kurar (`OVERPASS_MODE=init`), sonra Geofabrik'in günlük farklarıyla
   güncellenir. Uç adresi `http://overpass/api/interpreter`, dışarıdan `localhost:12345`.

Hazırlanmış veri birimlerde (`osmdata`, `overpassdb`) kalır. Tekrar açılışta indirme ve hazırlık atlanır;
zorlamak için `FORCE=1` verilir.

| Değişken | Varsayılan | Ne |
| --- | --- | --- |
| `PBF_URL` | Geofabrik `turkey-latest.osm.pbf` | OSRM için kaynak veri |
| `BBOX` | `28.45,40.80,29.45,41.30` | Kırpma kutusu (batı,güney,doğu,kuzey) |
| `OSRM_TAG` | `v5.27.1` | OSRM imaj sürümü |
| `OVERPASS_PLANET_URL` | Geofabrik `turkey-latest.osm.bz2` | Overpass ilk veri |
| `OVERPASS_DIFF_URL` | Geofabrik `turkey-updates/` | Günlük güncelleme |
| `OVERPASS_TAG` | `latest` | Overpass imajı. Yayında sabit bir sürüme bağlayın. |

### Kaynak ihtiyacı (kaba)

| Hizmet | Bellek | Disk | İlk hazırlık |
| --- | --- | --- | --- |
| OSRM hazırlığı (İstanbul kırpılmış, profil başına) | 2–4 GB | ~1 GB | dakikalar |
| OSRM sunucusu (profil başına) | ~1 GB | — | — |
| Overpass (Türkiye) | 2–4 GB | 15–30 GB | saatler |

Overpass Türkiye'nin tamamını yükler. Daha küçük bir bölge isteniyorsa `OVERPASS_PLANET_URL` daha küçük bir
özütle değiştirilir.

## API ayarları

Override dosyası API'ye şu ayarları geçirir:

| Ayar | Değer |
| --- | --- |
| `Routing:FootBaseUrl` | `http://osrm-foot:5000/` |
| `Routing:CarBaseUrl` | `http://osrm-car:5000/` |
| `Overpass:Endpoints:0` | `http://overpass/api/interpreter` |
| `Overpass:Endpoints:1` | `https://overpass-api.de/api/interpreter` (yedek) |

- Kendi Overpass'ımız 3,5 sn içinde yanıt vermezse sıradaki uç paralel başlar (`OverpassPlacesClient`).
- Boş ya da geçersiz adres genel sunucuya düşer (`RoutingOptions`, `OverpassOptions`).
- Gerçek yaya profilinde süre zaten gerçekçidir. Araç hızına göre düzeltme yalnızca genel sunucuda devreye girer
  (`OsrmRoute`).

## Doğrulama

```bash
curl "http://localhost:5001/route/v1/walking/29.0250,40.9885;29.0290,40.9905?overview=false"
curl "http://localhost:5002/route/v1/driving/29.0250,40.9885;29.0290,40.9905?overview=false"
curl --data-urlencode 'data=[out:json];node["amenity"="cafe"](around:500,40.9885,29.0250);out 5;' http://localhost:12345/api/interpreter
```

Yaya rotasında `duration / distance` yaklaşık 0,7–0,9 sn/m çıkmalıdır (~4–5 km/sa).

## Durum

- **Doğrulanan:** `docker compose ... config` ile birleşik yapılandırma, ayarların API'ye bağlanması ve uç seçimi
  (`ConfigurableEndpointsTests`).
- **Doğrulanmayan:** Konteynerlerin gerçekten ayağa kalkması. Geliştirme ortamında Docker servisi çalışmıyordu.
  İlk kurulumda yukarıdaki üç `curl` ile kontrol edin.
