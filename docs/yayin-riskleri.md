# Yayın riskleri ve doğrulanmamış varsayımlar

Bu dosya kodda alınan kararları ve **doğrulanamayan** noktaları listeler. "Doğrulanmadı" yazan her madde, resmi
kaynaktan insan tarafından kontrol edilmeden kesin kabul edilmemelidir. Bu çalışma sırasında Google ve OSM politika
sayfalarına bu ortamdan erişilemedi (DNS/vekil engeli); bu yüzden politika kararları **temkinli tarafta** alındı.

## Android izinleri (manifest)

Production prebuild çıktısında (`npx expo prebuild -p android`, depoya eklenmedi) kalan izinler ve gerekçeleri:

| İzin | Kaynak | Gerekçe |
| --- | --- | --- |
| `ACCESS_COARSE_LOCATION` | `expo-location` (`apps/nereye-go/lib/geo.ts`) | Yakındaki mekanları önermek; yalnızca uygulama açıkken, izin isteğiyle |
| `ACCESS_FINE_LOCATION` | `expo-location` (`apps/nereye-go/lib/geo.ts`) | Yürüme mesafesindeki (~800 m) öneriler ve rota başlangıcı için yaklaşık konum yetmez |
| `INTERNET` | React Native | API'ye bağlanmak |
| `ACCESS_NETWORK_STATE` | `expo-image` kütüphane manifesti | Görsel yükleyicinin ağ durumunu okuması; kişisel veri değil |
| `VIBRATE` | `expo-haptics` (`apps/nereye-go/lib/haptics.ts`) | Dokunsal geri bildirim |

`apps/nereye-go/app.json` → `android.blockedPermissions` ile **kaldırılanlar** (`tools:node="remove"`):

| İzin | Neden kaldırıldı |
| --- | --- |
| `CAMERA`, `RECORD_AUDIO` | Uygulama kamera ya da mikrofon kullanmıyor; `expo-image-picker` eklentisinde `cameraPermission: false`, `microphonePermission: false` |
| `READ_EXTERNAL_STORAGE`, `WRITE_EXTERNAL_STORAGE`, `READ_MEDIA_IMAGES`, `READ_MEDIA_VIDEO`, `READ_MEDIA_VISUAL_USER_SELECTED` | Fotoğraf seçimi Android'in sistem seçicisiyle (PickVisualMedia) yapılır, galeri izni gerekmez. Kodda `requestMediaLibraryPermissionsAsync` yok (test: `lib/play-policy.test.ts`) |
| `SYSTEM_ALERT_WINDOW` | Kullanılmıyor (geliştirme aracının izni) |
| `ACCESS_BACKGROUND_LOCATION` | Arka planda konum kullanılmıyor |

**Doğrulanmadı:** iOS tarafı (izin metinleri, fotoğraf seçici, harita) denenmedi; iOS derlemesi alınmadı.

## Hedef API seviyesi

- `targetSdkVersion` 36 olarak sabitlendi (`apps/nereye-go/app.config.ts`, `TARGET_SDK`). React Native 0.88'in
  varsayılanı da 36. **Play'in bugünkü asgari hedef API şartı doğrulanmadı**; insan Play Console'da kontrol eder.

## Harita ve konum verisi politikaları

- **OSM döşeme sunucusu:** Uygulama artık public OSM döşemesi kullanmıyor; Android'de Google Haritalar
  (`react-native-maps`, `PROVIDER_GOOGLE`). Anahtar (`GOOGLE_MAPS_ANDROID_API_KEY`) verilmezse harita
  "Harita anahtarı yok" der; OSM'e sessiz geri düşüş yok (test: `lib/map-policy.test.ts`). OSM Tile Usage
  Policy'nin güncel metni **doğrulanmadı**.
- **Nominatim:** Sunucu tüm Nominatim isteklerini süreç genelinde saniyede en fazla 1 istekle sınırlar
  (`NominatimGate`), öneri sonuçlarını 7 gün önbelleğe alır, istemci başına `geo` hız sınırı 30/dk. Kimlik:
  `User-Agent: Nereye/1.0 (iletişim)`; iletişim `Places:Contact` ya da `NEREYE_CONTACT` ile insan tarafından
  verilir, boşsa yalnızca `Nereye/1.0`. Nominatim kullanım şartlarının güncel hâli (iletişim zorunluluğu,
  otomatik tamamlama yasağı) **doğrulanmadı**. Yayında kendi geocoder'ını barındırmak ya da ticari sağlayıcıya
  geçmek insan kararıdır.
- **Google Places içerik saklama:** Liste isteği yalın alan maskesi kullanır; özet/yorum/olanak (Atmosphere)
  yalnızca mekan detayında istenir. Önbellek süreleri: fotoğraf adresi 30 dk, yakındaki arama 6 sa, zengin detay
  24 sa, OSM yakın arama 3 gün, OSM detay 7 gün (`src/Nereye.Domain/Places/PlacesCacheTtl.cs`). Google Maps
  Platform şartlarındaki saklama sınırları **doğrulanmadı**; 6 sa ve 24 sa değerleri önceki sürümden kaldı.
- **iOS haritası:** iPhone'da harita her zaman görünür. Derlemeye `GOOGLE_MAPS_IOS_API_KEY` verilirse Google
  haritası, verilmezse Apple Haritalar kullanılır (Expo Go'da da). Apple haritasının üstünde Google'dan gelen
  mekanların pinleri ve kartları görünür; Google Maps Platform şartları Google içeriğinin Google dışı haritada
  gösterilmesini kısıtlayabilir (**doğrulanmadı**). Yayın öncesi iOS derlemesine Google anahtarı verilmesi önerilir.
- **Android haritası:** Production derlemesi `GOOGLE_MAPS_ANDROID_API_KEY` olmadan derlenmez (harita boş kalmasın).
- **Atıf:** Harita altında "Harita © Google · Mekan verisi © OpenStreetMap katkıcıları"; Google yorumlarında yazar
  adı Google profil bağlantısına gider.

## Hesap, e-posta ve içerik

- **E-posta sağlayıcısı** (`src/Nereye.Infrastructure/Email/EmailSenders.cs`): Resend ve Postmark HTTP API'leri
  belgelere göre yazıldı, **gerçek hesapla denenmedi**. Anahtar yoksa doğrulama, şifre sıfırlama ve web'den hesap
  silme 503 ile kapalı kalır.
- **E-posta doğrulaması zorunlu:** Yorum, yanıt, yorum/işletme fotoğrafı ve profil fotoğrafı yalnızca doğrulanmış
  hesaptan. **Bu sürümden önce açılmış hesaplar doğrulanmamış sayılır**; yazmak için bir kez doğrulama
  bağlantısı istemeleri gerekir. E-posta sağlayıcısı kurulmadan yayına çıkılırsa **kimse yorum yazamaz**.
- **Şifre sıfırlama zamanlaması:** Kayıtlı ve kayıtsız adres için cevap gövdesi ve durum kodu aynı (test var).
  Kayıtlı adreste e-posta gönderildiği için cevap süresi farklı olabilir; zamanlama farkı ölçülmedi.
- **Hukuk metni** (`src/Nereye.Api/LegalTexts.cs`): taslaktır. İletişim adresi `Legal:Contact` boşsa
  "İletişim adresi tanımlanmadı" yazar. "18 yaş" şartı Play hedef kitle kararına göre yazıldı; avukat onayı yok.
- **Ödeme:** Uygulama içi satın alma yok. Öne çıkarma talebi `EXPO_PUBLIC_PROMOTION_CONTACT_URL` ile iletişime
  yönlendirir; sandbox ödeme yalnızca Development/Testing'de. Fiziksel olmayan dijital hizmet satışının Play
  Billing şartı kapsamı **doğrulanmadı**; şu an satış yapılmadığı için tetiklenmiyor.

## Paylaşım ve derin bağlantı

- Paylaşım metni `nereyego://venue/{id}` taşır. Birçok mesajlaşma uygulaması özel şemayı tıklanabilir yapmaz;
  metin mekan adı ve adresiyle yine okunur. https bağlantısı yalnızca insan `EXPO_PUBLIC_SHARE_BASE_URL`
  verirse eklenir; o adreste `/venue/{id}` için Android App Links (assetlinks.json) kurulumu insan işidir.

## Bağımlılıklar

- **npm audit** (`apps/nereye-go`): critical 0 (`shell-quote` `overrides` ile 1.12.0'a yükseltildi).
  Kalan 33 "high", hepsi `picomatch` ReDoS açığından (GHSA-fc5q-39xc-vjpf, ≤4.0.7) zincirleme: Expo CLI,
  Metro, jest-util, tinyglobby. **Yamalı picomatch sürümü yayımlanmamış** (en son 4.0.7). Bunlar derleme
  araçlarıdır, uygulama paketine girmez; `npm audit fix --force` Expo'yu 50'ye düşürmeyi önerdiği için
  uygulanmadı. 4 "moderate": `minimatch` (GHSA-g3hv-75wg-m249, düzeltmesi major sürüm) ve `uuid` 3.4.0
  (GHSA-w5hq-g745-h8pq).
- **NuGet:** `System.Security.Cryptography.Pkcs` 10.0.15'e sabitlendi; `dotnet list package --vulnerable` temiz.
- **SkiaSharp** (MIT) görsel küçültme ve EXIF temizliği için eklendi; Linux'ta
  `SkiaSharp.NativeAssets.Linux.NoDependencies` ile çalışır. Docker imajında çalıştırma **denenmedi** (bu
  ortamda Docker yok); yerel Linux'ta testler geçiyor.

## Veritabanı

- `TimestampTz` migration'ı tüm zaman sütunlarını dönüştürür ve tabloyu yeniden yazar. Gerçek Postgres 16'ya karşı
  test edildi (`PostgresMigrationTests`); **yayındaki veriyle denenmedi**. Uygulamadan önce yedek alınmalı.

## Ayrık commit

- `origin/main` üzerindeki `b8863aa` ("Harita/rota ve mağaza görselleri; API öneri ve OSRM iyileştirmeleri")
  bu dalda yok. Bu çalışmada birleştirilmedi; **ayrık commit, sonra elle bakılacak.** Özellikle harita/rota
  değişiklikleri bu daldaki Google harita geçişiyle çakışabilir.
