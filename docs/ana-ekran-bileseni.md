# Ana ekran bileşeni (Android)

"Yakındaki açık mekanlar" bileşeni ana ekranda son seçilen niyetin ilk üç açık mekanını gösterir. Satıra
dokununca mekan sayfası, başlığa dokununca uygulama açılır.

## Nasıl çalışır

1. Uygulama ana sayfa listesini aldığında (yalnızca Android) bir anlık görüntü yazar:
   `lib/widget-data.ts` → `widgetSnapshot(...)` → `setLargeJson('widget-nearby', …)`.
   Dosya `context.filesDir/widget-nearby.json` olur (`expo-file-system` `Paths.document`).
2. Anlık görüntüde yalnızca şunlar var: başlık ("Kahve · Kadıköy"), güncelleme saati ve en çok üç mekanın
   kimliği, adı ve kısa satırı ("23:00'e kadar açık · 5 dk"). Sponsorlu kart ve kapalı mekan girmez.
   Konum ya da hesap bilgisi yazılmaz; dosya cihazdan çıkmaz.
3. Bileşen (`NearbyWidgetProvider`, Kotlin) bu dosyayı okur ve `RemoteViews` ile çizer.
   - Android bileşeni en sık 30 dakikada bir yeniler (`updatePeriodMillis=1800000`).
   - Bileşen kendisi ağdan veri çekmez. Yeni veri uygulama açılıp ana liste yüklenince gelir; ekranda görünmesi
     en fazla bir yenileme turu (30 dk) sürebilir.
   - Anlık görüntü 6 saatten eskiyse liste yerine "Güncel liste için Nereye'yi aç." yazar.
4. Satır bağlantıları yalnızca `nereyego://venue/{id}` biçimindedir, `PendingIntent.FLAG_IMMUTABLE` ile.
   Alıcı `android:exported="false"`.

## Dosyalar

| Yol | Ne |
| --- | --- |
| `apps/nereye-go/plugins/withNearbyWidget.js` | Expo config plugin. Prebuild'de Kotlin sınıfını, yerleşimi, `appwidget-provider` bilgisini, çizimleri ve Türkçe/İngilizce metinleri üretir; manifest'e alıcıyı ekler. |
| `apps/nereye-go/lib/widget-data.ts` | Anlık görüntüyü kartlardan üretir (saf, testli). |
| `apps/nereye-go/lib/widget-data.test.ts` | Seçim kuralları, metin ve saat. |
| `apps/nereye-go/lib/widget-plugin.test.ts` | Üretilen Kotlin/XML'in temel özellikleri ve manifest girdisi. |

Plugin `app.json` içinde `"./plugins/withNearbyWidget"` olarak kayıtlıdır. `android/` klasörü repoda yok;
her yerel derlemede prebuild ile yeniden üretilir.

## Doğrulama

- **Doğrulanan:**
  - Birim testleri (`npm test`) ve tip denetimi.
  - Geçici bir kopyada `npx expo prebuild --platform android --clean` başarıyla bitti. Kotlin dosyası,
    yerleşim, `xml/nereye_nearby_widget_info.xml`, çizimler, `values`/`values-en` metinleri ve manifest alıcısı
    üretildi.
- **Doğrulanmayan:**
  - Kotlin kodu derlenmedi ve bileşen gerçek bir cihazda ya da öykünücüde denenmedi. Bu ortamda Android SDK
    yok; EAS derlemesi de bilerek çalıştırılmadı.
  - İlk yerel derlemede bileşen seçicide "Nereye" altında görünmeli, ana listeyi açtıktan sonra en geç 30 dk
    içinde dolmalı ve satıra dokununca mekan sayfası açılmalıdır.

## Yapılmayanlar

- **iOS:** WidgetKit uzantısı (Swift), App Group ve uygulamanın paylaşılan kapsayıcıya yazması gerekir.
  Yapılmadı.
- **Anında yenileme:** Uygulama dosyayı yazınca bileşene yayın göndermiyor. Bunun için küçük bir yerel modül
  (`AppWidgetManager.notifyAppWidgetViewDataChanged` / `ACTION_APPWIDGET_UPDATE`) gerekir.
- **Arka planda veri çekme:** Bileşen konum alıp sunucuya sormuyor; bu, arka plan konum izni ve pil maliyeti
  demek. Bilerek dışarıda bırakıldı.
