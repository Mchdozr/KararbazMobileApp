# Uçtan uca testler

## Web (Playwright) — CI'da her push'ta

```bash
cd e2e
npm ci
npx playwright install chromium   # bu geliştirme ortamında gerekmez, tarayıcı hazır
npx playwright test
```

Yapılandırma (`e2e/playwright.config.ts`) iki sunucuyu kendisi başlatır:

- **API** `localhost:5090`: sahte mekan verisi, bellek içi veritabanı ve önbellek. Postgres ve Redis gerekmez.
- **Web derlemesi** `localhost:8090`: `expo export -p web` çıktısı `e2e/.web` klasörüne yazılır.

Yerelde tekrar çalıştırırken derlemeyi atlamak için `E2E_SKIP_EXPORT=1 npx playwright test` kullanın. Portlar
`E2E_API_PORT` ve `E2E_WEB_PORT` ile değişir.

| Dosya | Akış |
| --- | --- |
| `home.spec.ts` | Öneri kartları gerçek API'den gelir; "Benim için seç" gerekçeli seçim yapar, "Başka öner" ve kapatma çalışır; karttan mekan sayfasına geçilir. |
| `discover.spec.ts` | Niyet hızla değişince eski istekler ağda iptal edilir, liste son niyete aittir. |
| `privacy.spec.ts` | İzin yokken kullanım olayı gitmez; izinden sonra kimliksiz olay gider; reddedilen kart bir daha çıkmaz; uygulama hatası kişisel veri ayıklanarak raporlanır. |
| `search-auth.spec.ts` | Arama sonuç getirir; oturumlu kullanıcı adını görür, mekan kaydeder (API'de doğrulanır). |

Web'de harita yer tutucudur. Harita akışları Maestro tarafında.

## Yerel uygulama (Maestro) — cihazda ya da emülatörde

Akışlar `apps/nereye-go/.maestro/` klasöründedir:

| Akış | Ne |
| --- | --- |
| `01-kesfet` | Karşılama ekranından listeye geçiş |
| `02-benim-icin-sec` | "Benim için seç" |
| `03-mekan-detay` | Mekan sayfası |
| `04-gizlilik` | Kullanım istatistiği izni varsayılan kapalı, reddedilince bir daha sorulmaz |

Bir geliştirme derlemesi (`npx expo run:android`) cihaza ya da emülatöre kurulduktan sonra:

```bash
cd apps/nereye-go
maestro test .maestro
```

Sahte veriyle çalıştırmak için derleme `EXPO_PUBLIC_API_URL`, sahte veriyle başlatılmış API'yi göstermelidir
(`CLAUDE.md` içindeki komut).

**Durum:** Maestro akışları yazıldı ama bu geliştirme ortamında çalıştırılamadı (emülatör ya da cihaz yok).
CI'ya da eklenmedi: Android emülatörü GitHub'ın standart çalıştırıcısında yavaş ve kararsızdır. İlk
çalıştırmada seçici düzeltmesi gerekebilir.
