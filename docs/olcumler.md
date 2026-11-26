# Ölçümler

İki katman var:

1. **Süreç içi özet:** her zaman açık. `MetricsCollector` son 15 dakikayı tutar. API özeti 30 saniyede bir
   Redis'e yazar (`metrics:api:latest`). Yönetim panelindeki **Ölçümler** sayfası oradan okur. Aynı özet,
   yönetici token'ıyla `GET /api/admin/metrics` ucunda da var.
2. **OpenTelemetry (OTLP):** yalnızca bir uç verilince açılır. `OTEL_EXPORTER_OTLP_ENDPOINT` ya da
   `OpenTelemetry:Endpoint` ayarı kullanılır, örnek `http://otel-collector:4317`. Ölçümler ve izler toplayıcıya
   gider (Grafana/Prometheus, Honeycomb, Datadog…). Servis adı `OpenTelemetry:ServiceName`, varsayılanı `nereye-api`.

## Ne ölçülüyor

| Ölçüm | Kaynak | Etiketler |
| --- | --- | --- |
| `http.server.request.duration` | ASP.NET Core | `http.route`, `http.request.method`, `http.response.status_code` |
| `nereye.cache.lookups` | Öneri listesi (`nearby`) ve OSM katmanı (`osm`) | `layer`, `result` = `hit`, `stale` (sunuldu, arkada tazelendi) ya da `miss` |
| `nereye.upstream.duration` (ms) | Her dış HTTP çağrısı (`UpstreamMetricsHandler`) | `service` = `overpass`, `google`, `osrm`, `nominatim`, `wikimedia`, `website` ya da `mapillary`; `outcome` = `ok`, `limited` (429), `error`, `timeout` ya da `cancelled` |

`cancelled`, paralel yedek uç kazandığında ya da istemci vazgeçtiğinde yazılır. Hata sayılmaz.

## Panelde nasıl okunur

| Bölüm | Gösterdiği | Uyarı |
| --- | --- | --- |
| Uçlar | p50/p95/p99 ve 5xx sayısı. Statik dosyalar tek satırda toplanır. | p95 1,5 sn'yi aşan satır sarı |
| Dış servisler | Çağrı sayısı, hata ve 429 oranı, p95 | Hata + 429 %10'u aşarsa satır kırmızı. Genel Overpass ya da OSRM sınırına takılıyorsanız `docs/kendi-sunucular.md`. |
| Önbellek | İsabet oranı. Bayat kayıt kullanıcıya beklemeden sunulduğu için isabet sayılır. | — |

## Sınırlar

- Özet örnek başınadır. Birden çok API örneği aynı Redis'e yazarsa panel son yazanı gösterir. Toplam görünüm için
  OTLP kullanın.
- Pencere 15 dakikadır. Geçmiş trend yalnızca OTLP tarafında tutulur.

## Doğrulama

Gerçek bir OpenTelemetry Collector ile denendi (2026-11-26):
- Kurulum: `otel/opentelemetry-collector-contrib:0.120.0`, OTLP gRPC 4317, `debug` dışa aktarıcısı.
- API ayarları: `OpenTelemetry__Endpoint=http://localhost:4317` ve `OTEL_METRIC_EXPORT_INTERVAL=5000`.
- Gelen ölçümler: `nereye.upstream.duration` (ör. `service=overpass, outcome=ok`), `nereye.cache.lookups` ve
  `http.server.request.duration`.
- Gelen izler: `api/recommendations` ve `api/routes`, dış çağrı alt aralıklarıyla birlikte
  (`url.full=http://localhost:12345/api/interpreter`).
- Kaynakta `service.name=nereye-api` var.

Kalıcı bir toplayıcı (Grafana, Honeycomb, Jaeger…) seçimi yayın kararıdır. Yukarıdaki ayar herhangi bir OTLP
uyumlu hedefe çalışır.
