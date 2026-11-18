# Veritabanı şemasını güncelleme

API ve yönetim paneli yayında açılırken migration **çalıştırmaz**. Şema, dağıtımdan önce tek seferlik ayrı bir adımla güncellenir. Böylece:

- Birden fazla API örneği aynı anda açılırken migration yarışına girmez.
- Şema güncel değilse uygulama yarım şemayla çalışmaz; açılışta hangi migration'ın beklediğini yazıp durur.

## Adım

İkisinden biri, her dağıtımda bir kez:

```bash
# Geliştirici makinesinden (dotnet-ef aracıyla)
dotnet ef database update --project src/Nereye.Infrastructure --startup-project src/Nereye.Api
```

```bash
# Yayımlanmış imaj içinden (SDK gerekmez)
dotnet Nereye.Api.dll migrate
```

`docker-compose.yml` bunu `migrate` servisi olarak yapar; `api` ve `admin` bu servis başarıyla bitince açılır.

İki `migrate` adımı yanlışlıkla aynı anda çalışsa da EF Core migration sırasında veritabanı kilidi alır; aynı migration iki kez uygulanmaz. Tohum verisi (roller, admin hesabı) açılışta bir Postgres oturum kilidi (`pg_advisory_lock`) altında yazılır.

## Ayarlar

| Ayar | Varsayılan | Anlamı |
| --- | --- | --- |
| `Database:MigrateOnStartup` | Development'ta `true`, diğerlerinde `false` | Açılışta migration çalışsın mı |

## timestamptz geçişi (`TimestampTz`)

Eski sürümde zaman sütunları `timestamp without time zone` idi (Npgsql "legacy timestamp" anahtarı). `TimestampTz` migration'ı hepsini `timestamptz` yapar ve dönüşümü `SET TimeZone = 'UTC'` ile yapar: eski değerler uygulama tarafından `UtcNow` ile yazıldığı için UTC kabul edilir. Anahtar kaldırıldı; `UtcDateTimeConverter` her `DateTime`'ı UTC olarak okur/yazar.

Bu geçiş `tests/Nereye.Api.Tests/PostgresMigrationTests.cs` ile gerçek Postgres'e karşı denenir (oturum saat dilimi bilerek İstanbul yapılır; değerler kaymamalı).

**Yayındaki veritabanında uygulamadan önce yedek alın.** Büyük tablolarda `ALTER COLUMN ... TYPE` tabloyu yeniden yazar ve kilitler; bakım penceresinde çalıştırın.
