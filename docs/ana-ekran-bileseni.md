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

- **Birim testleri** (`npm test`) ve tip denetimi.
- **Android derlemesi ve çizim testi** (`apps/nereye-go/plugins/widget-check`, CI'da `android-widget` işi):
  - Eklentinin ürettiği Kotlin/XML, uygulamayla aynı sürümlerle derlenir: AGP 9.2.1, compileSdk 37, minSdk 24.
  - Robolectric testlerinin okuduğu örnek veri, uygulamanın `widgetSnapshot()` çıktısıdır.
  - 8 test: taze, bayat ve bozuk veri; satır ve başlık bağlantıları; İngilizce cihaz; `AppWidgetManager`
    güncellemesi; yerel grafikle PNG önizleme. Lint 0 hata.

  ```bash
  cd apps/nereye-go && node --experimental-strip-types plugins/widget-check/generate.mts
  cd plugins/widget-check && gradle testDebugUnitTest lintDebug    # önizleme: build/widget-preview-*.png
  ```

- **Tam uygulama derlemesi** (2026-11-26): `expo prebuild` ve ardından
  `./gradlew assembleDebug -PreactNativeArchitectures=x86_64` başarılı oldu (6 dk 43 sn, 84 MB APK). APK'da:
  - manifest'te `com.mchdozr.nereye.widget.NearbyWidgetProvider` alıcısı, `exported=false`, `APPWIDGET_UPDATE`;
  - dex içinde sınıf;
  - `layout/nereye_nearby_widget`, `xml/nereye_nearby_widget_info` ve Türkçe/İngilizce metinler.

  Bu ortama özel iki not:
  - Maven Central geçici olarak 429 döndüğü için derleme Google'ın Maven Central aynasına yönlendirildi.
    Bu yönlendirme repoya girmeyen bir Gradle init betiğiyle yapıldı.
  - Robolectric'in yerel çalışma zamanı jar'ı elle indirildi ve SHA1'i Maven Central'daki ile karşılaştırıldı.
- **Doğrulanmayan:** bileşenin gerçek bir ana ekranda görünmesi. Geliştirme konteynerinde KVM olmadığı için
  emülatör açılamıyor; bağlı cihaz da yok. İlk denemede şunlar beklenir:
  - Bileşen seçicide "Nereye" altında görünür.
  - Ana listeyi açtıktan sonra en geç 30 dk içinde dolar.
  - Satıra dokununca mekan sayfası açılır.

## Yapılmayanlar

- **iOS:** WidgetKit uzantısı (Swift), App Group ve uygulamanın paylaşılan kapsayıcıya yazması gerekir.
  Yapılmadı.
- **Anında yenileme:** Uygulama dosyayı yazınca bileşene yayın göndermiyor. Bunun için küçük bir yerel modül
  (`AppWidgetManager.notifyAppWidgetViewDataChanged` / `ACTION_APPWIDGET_UPDATE`) gerekir.
- **Arka planda veri çekme:** Bileşen konum alıp sunucuya sormuyor; bu, arka plan konum izni ve pil maliyeti
  demek. Bilerek dışarıda bırakıldı.
