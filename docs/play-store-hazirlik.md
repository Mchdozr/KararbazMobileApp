# Nereye — Google Play'e hazırlık promptu

> Bu dosya bir yapay zekâ kodlama ajanına (Claude Code, Cursor vb.) olduğu gibi verilmek üzere yazıldı.
> Kod tabanının 13 Kasım 2026 tarihli hâline (`claude/adoring-ramanujan-1tepls`, `9d46ca9`) dayanır.
> Politika ve sınır değerleri (hedef API seviyesi, test kullanıcısı sayısı, Google Maps şartları)
> zamanla değişir: ajan her birini uygulamadan önce güncel resmi kaynaktan doğrulamalıdır.

---

## PROMPT

Sen Nereye projesinin kıdemli mobil + backend mühendisisin. Görevin, uygulamayı **Google Play'de
herkese açık yayına çıkarılabilir** hâle getirmek. "Çalışıyor" yetmez: mağaza incelemesinden geçmeli,
yasal olarak savunulabilir olmalı, gerçek kullanıcı yükünde ayakta kalmalı ve ilk hafta çökmemeli.

### Bağlam

- Monorepo. Canlı istemci `apps/nereye-go` (Expo SDK 58, expo-router, React Native 0.88, TypeScript).
  Backend `src/Nereye.Api` (ASP.NET Core 10, EF Core + PostgreSQL, Redis, JWT). Yönetim paneli `src/Nereye.Admin` (Blazor).
  `apps/mobile` eski Flutter arşividir.
- Mekan kataloğu OpenStreetMap (Overpass + Nominatim); Google Places (New) üstüne puan, özet, yorum, fotoğraf ekler.
- Kökteki `CLAUDE.md` ve `apps/nereye-go/AGENTS.md` bağlayıcıdır. Önce ikisini oku.
- Doğrulama komutları: `dotnet test Nereye.slnx`, `cd apps/nereye-go && npm test && npm run typecheck`.

### Çalışma kuralları

1. Bana soru sorma. Her açık kararı aşağıdaki **"Alınmış kararlar"** bölümüne göre ver; orada yoksa
   en güvenli ve en basit seçeneği seç, kararı commit mesajına bir cümleyle yaz.
2. Fazları sırayla yap. Her fazın **kabul kriterleri** karşılanmadan sonrakine geçme.
3. Davranış değişikliği = önce test. Her commit'ten önce üç doğrulama komutu yeşil olmalı.
   Arayüz değiştiyse web derlemesinde Playwright ile ekran görüntüsü al ve kendin incele.
4. Küçük, konu başına bir commit. Mesaj Türkçe, "ne" değil "neden".
5. Sır (API anahtarı, şifre, keystore) asla repoya girmez. `.env`, EAS secrets, `dotnet user-secrets` kullan.
6. Yapamadığın, insan gerektiren her işi (hesap açma, ödeme, hukuk onayı) uydurmadan
   `docs/yayin-insan-gorevleri.md` dosyasına yaz ve devam et.
7. Bir dış servisin politikasından emin değilsen (Google, OSM, Play) resmi sayfasını oku. Okuyamıyorsan
   riski `docs/yayin-riskleri.md` dosyasına "doğrulanmadı" diye yaz; varsayımı gerçekmiş gibi sunma.

---

### FAZ 0 — Yayını doğrudan engelleyenler

Bunlardan biri bile kalırsa uygulama ya reddedilir ya da yayından sonraki gün çalışmaz.

**0.1 Kalıcı, HTTPS'li bir API adresi**
- `apps/nereye-go/eas.json` production ortamında API adresi bir `trycloudflare.com` geçici tünelidir.
  Tünel kapanınca yayındaki her kurulum ölür. Kalıcı bir alan adına (ör. `api.nereye.app`) geçir.
- `lib/constants.ts` içindeki `DEFAULT_API` geri dönüşü `http://192.168.1.248:5079`. Production derlemesinde
  `EXPO_PUBLIC_API_URL` yoksa derleme **başarısız olsun** (ör. `app.config.ts` içinde kontrol); LAN adresine düşmesin.
- `app.json`: `android.usesCleartextTraffic: true` ve iOS `NSAllowsArbitraryLoads: true` production'da kalkacak.
  Geliştirmede LAN HTTP gerekiyorsa bunu yalnızca development profiline koy (`app.config.ts` + `APP_VARIANT`).
- Kabul: production AAB'de cleartext kapalı; API adresi `https://` ile başlıyor; giriş ekranındaki gizli
  "sunucu adresi" alanı production derlemesinde hiç görünmüyor.

**0.2 Harita döşemeleri ve OSM servis politikaları**
- `app/(tabs)/map.tsx` ve `app/venue/[id].tsx` doğrudan `tile.openstreetmap.org` kullanıyor. OSM Tile Usage
  Policy ağır uygulama kullanımını yasaklar; yayında engellenme riski yüksek.
- `NominatimPlacesClient.SuggestAsync` konum arama kutusunda her tuş vuruşunda (280 ms gecikmeyle) public
  Nominatim'e gidiyor. Nominatim Usage Policy saniyede en fazla 1 istek ister ve istemci tarafı otomatik
  tamamlamayı yasaklar.
- Overpass'ın public sunucuları da üretim yükü için değildir.
- Yapılacak: tile için ücretli/kotalı bir sağlayıcı (MapTiler, Stadia, Thunderforest) **ya da** Android'de
  Google Maps (bkz. 0.3) seç. Otomatik tamamlama için Photon (komoot) ya da kendi barındırdığın bir geocoder
  kullan; sunucuda sonuçları önbelleğe al ve istemci başına hız sınırı uygula. Overpass için kendi örneğini
  barındırmayı ya da sonuçları uzun TTL ile önbelleklemeyi değerlendir.
- Kabul: kodda `tile.openstreetmap.org` geçmiyor; sağlayıcı anahtarı sırlarda; atıf metni ("© OpenStreetMap
  katkıcıları" + sağlayıcı) haritada görünüyor.

**0.3 Google Maps Platform kullanım şartları**
- Google Places içeriği (puan, yorum, fotoğraf, özet) şu an OSM döşemeli bir haritanın üstünde ve yanında
  gösteriliyor. Google Maps Platform şartları Places içeriğinin Google dışı bir haritada gösterilmesini
  kısıtlar, içeriğin önbelleklenmesini sınırlar (place ID hariç) ve atıf ister.
- Önce güncel şartları (Maps Service Specific Terms, Places API policies) oku ve `docs/yayin-riskleri.md`
  dosyasına uyum tablosu yaz. Sonra şu iki yoldan birini uygula:
  - **A (önerilen):** Harita sekmesinde ve detaydaki mini haritada Android için Google Maps sağlayıcısı
    (`react-native-maps` `PROVIDER_GOOGLE`, `android.config.googleMaps.apiKey` EAS secret olarak). Anahtarı
    paket adı ve SHA-1 ile kısıtla. Böylece Google verisi Google haritasında görünür.
  - **B:** Google kaynaklı içeriği (puan/yorum/foto) OSM haritasının üstünde göstermeyi bırak; yalnızca
    detay sayfasında harita olmadan göster.
- Yorumlarda `authorAttribution.uri` (yazarın profil bağlantısı) şu an atılıyor; şartlar bunu istiyorsa geri
  ekle ve yazar adını bağlantı yap. Google içeriği gösterilen her yerde gerekli atıfı ("Google Haritalar")
  koy.
- Sunucudaki önbellek sürelerini (`PlacesCacheTtl`) şartlara göre düzelt.
- Kabul: uyum tablosunun her satırında "uyumlu" ya da "insan kararı gerekiyor" yazıyor; kod tabloyla tutarlı.

**0.4 Android izinleri ve hedef SDK**
- Play'in o tarihteki zorunlu `targetSdkVersion` değerini doğrula (2026 sonu için en az 35, büyük olasılıkla 36).
  `expo-build-properties` ile açıkça ayarla.
- `npx expo prebuild --platform android --clean` ile birleşik manifesti çıkar ve gerçekten eklenen izinleri listele.
  `READ_MEDIA_IMAGES`, `READ_EXTERNAL_STORAGE`, `CAMERA`, `RECORD_AUDIO` varsa ve kullanılmıyorsa
  `android.blockedPermissions` ile kaldır. Play, fotoğraf izinlerini çekirdek işlevi olmayan uygulamalarda
  reddeder; Android Photo Picker izinsiz çalışır.
- Konum yalnızca ön planda. `ACCESS_FINE_LOCATION` gerçekten gerekmiyorsa yalnızca `COARSE` bırak.
- Kabul: birleşik manifestteki her izin için `docs/yayin-riskleri.md` içinde bir gerekçe satırı var.

**0.5 Kullanıcı içeriği (UGC) politikası**
- Uygulamada kullanıcı yorumu ve fotoğrafı var. Play UGC politikası şunları ister: kullanım koşulları kabulü,
  uygunsuz içeriği bildirme, kullanıcı engelleme, aktif moderasyon.
- Var olan: yorum/mekan bildirme, admin panelde şikayet çözme ve yorum gizleme.
- Eksik ve yapılacak:
  - Kayıtta kullanım koşulları kabulü (KVKK onayından ayrı bir onay kutusu) ve barındırılan koşullar sayfası.
  - Bir kullanıcıyı engelleme: engellenenin yorumları engelleyene görünmez.
  - Fotoğraf bildirme.
  - Admin panelde bekleyen şikayet kuyruğu, tek tıkla gizle/yasakla, işlem kaydı.
  - Yüklenen fotoğraflar için en azından boyut/format doğrulaması ve otomatik içerik taraması (ör. bir
    moderasyon API'si) ya da yayına almadan önce onay kuyruğu.
- Kabul: API testleri bildirme, engelleme ve gizleme akışlarını kapsıyor.

**0.6 Ödeme**
- İşletme "öne çıkarma" kampanyası `SandboxPaymentGateway` ile alınıyor; gerçek para hareketi yok.
- Play'in Payments politikası uygulama içinde satılan dijital hizmetlerde Play Faturalandırma ister. İşletmeye
  satılan reklam/öne çıkarma hizmetinin bu kapsama girip girmediğini resmi metinden doğrula.
- Karar (v1): uygulama içindeki satın alma akışını kaldır. İşletme panelinde "Öne çıkarmak için bize ulaşın"
  bağlantısı kalsın; kampanyalar admin panelden açılsın. Sponsorlu kartlar ("Öne çıkan") kalabilir; bu durumda
  Play Console'da "Reklam içerir" beyanı yapılacak (bkz. Faz 5).
- Kabul: production derlemesinde ödeme ekranı yok; sandbox ödeme uç noktası production'da 404 dönüyor.

**0.7 Hesap silme**
- Uygulama içinde hesap silme var. Play ayrıca uygulamayı kurmadan da silme talebi yapılabilen bir **web
  adresi** ister. Basit bir sayfa yap: e-posta ile doğrulama bağlantısı gönderir, bağlantıya tıklanınca mevcut
  `DeleteMeAsync` çalışır. Hangi verinin silindiğini ve ne kadar süre tutulduğunu sayfada yaz.
- Kabul: web sayfası API testleriyle kapsanıyor; adres `docs/yayin-insan-gorevleri.md` içinde Play Console'a
  girilmek üzere not edildi.

---

### FAZ 1 — Yasal ve gizlilik

**1.1 Gizlilik politikası ve KVKK**
- `app/legal.tsx` içindeki metin bir taslaktır. Onu barındırılan bir web sayfasına taşı
  (ör. `https://nereye.app/gizlilik`); uygulama bu sayfayı açsın. Play bir gizlilik politikası adresi ister.
- KVKK'da aydınlatma bir bildirimdir, açık rıza ayrı bir onaydır. Kayıt ekranındaki "okudum ve kabul
  ediyorum" tek kutusunu ikiye ayır: aydınlatma metnine bağlantı (onay gerektirmez) ve yalnızca gerçekten
  açık rıza gereken işlemler için ayrı, önceden işaretlenmemiş kutular (ör. pazarlama bildirimleri).
- Yurt dışına veri aktarımı: Google API'lerine konum ve arama metni gidiyor. KVKK m.9 kapsamında
  değerlendirilmesi gereken konu olarak `docs/yayin-riskleri.md` dosyasına yaz. Bu bir hukuk kararıdır;
  metni sen kesinleştirme.
- Veri saklama süreleri: refresh token, şikayet, rıza kaydı, silinmiş hesap verisi için süre belirle ve bir
  arka plan işiyle uygula.
- Kabul: politika sayfası canlı; uygulama içi metin ve web metni aynı kaynaktan geliyor; rıza kayıtları amaç
  bazında tutuluyor (zaten var: `ConsentLog`).

**1.2 Play Console "Veri güvenliği" formu**
- Kodda gerçekten toplanan ve paylaşılan her veriyi çıkar: e-posta, ad, profil fotoğrafı, yaklaşık ve kesin
  konum, kullanıcı içeriği (yorum, fotoğraf), uygulama etkileşimleri, kilitlenme kayıtları (Faz 3'ten sonra),
  cihaz kimlikleri (varsa). Her biri için: zorunlu mu, neden, üçüncü tarafla paylaşılıyor mu (Google, harita
  sağlayıcı, Sentry), şifreli aktarılıyor mu, silinebiliyor mu.
- Bunu `docs/play-veri-guvenligi.md` dosyasına, Play Console formunun soru sırasıyla yaz.
- Kabul: tablodaki her satır koddaki bir dosyaya referans veriyor.

---

### FAZ 2 — Altyapı, güvenlik, işletim

**2.1 Barındırma**
- API ve Admin için kalıcı barındırma (Docker imajları zaten var). Yönetilen PostgreSQL (otomatik yedek, en az
  7 gün geri dönüş), yönetilen Redis, TLS sertifikası, sağlık kontrolü (`/health/live` ve `/health`).
- Kullanıcı yüklemeleri (`wwwroot/media/uploads`, `media/avatars`) şu an konteyner diskinde; her dağıtımda
  silinir. Nesne depolamaya taşı (S3 uyumlu: Cloudflare R2, AWS S3) ve CDN arkasından sun. Yüklemede görseli
  yeniden boyutlandır (ör. en fazla 1600 px) ve EXIF/konum verisini sil.
- Migration'lar şu an uygulama açılışında çalışıyor (`NereyeSeeder`). Birden fazla örnek açılınca yarışır.
  Dağıtım hattında ayrı bir adım yap (EF bundle ya da `dotnet ef database update`).
- README'de not edilen `timestamp` / Npgsql legacy switch: `timestamptz`'ye geçiş migration'ını yaz ve
  legacy anahtarını kaldır.
- Kabul: tek komutla (ya da CI ile) sıfırdan ortam kurulabiliyor; geri yükleme bir kez denendi ve
  `docs/operasyon.md` içinde adım adım yazılı.

**2.2 Güvenlik**
- Admin paneli internete açık olmasın: IP kısıtlaması ya da VPN/Cloudflare Access arkasında. Varsayılan admin
  şifresi production'da zaten reddediliyor; ilk kurulum akışını belgele.
- Google Places anahtarını Google Cloud'da yalnızca Places API (New) ile ve sunucu IP'siyle kısıtla; bütçe
  uyarısı ve API başına günlük kota tavanı koy. (Anahtar daha önce bir sohbette açık yazıldı: yenisini üret,
  eskisini iptal et.)
- JWT erişim süresi, refresh rotasyonu ve çalınan refresh token'ın yeniden kullanımını tespit (token family)
  ekle.
- Güvenlik başlıkları, CORS listesi, istek boyutu sınırları var; production yapılandırmasında doğrula.
- Bağımlılık taraması: NuGet uyarısı var (`System.Security.Cryptography.Pkcs` 9.0.0, yüksek önem). Güncelle.
  `npm audit` çıktısını incele ve kritik olanları kapat.
- Kabul: OWASP ASVS seviye 1 kontrol listesini `docs/guvenlik-kontrol.md` içinde madde madde işaretle.

**2.3 Gözlemlenebilirlik**
- İstemcide çökme raporlama: Sentry (`@sentry/react-native`, Expo yapılandırma eklentisi) ya da Firebase
  Crashlytics. Kaynak haritaları EAS derlemesinde yüklensin. Kişisel veri gönderme (`sendDefaultPii: false`).
- Backend'de yapılandırılmış log (Serilog, JSON), istek kimliği zaten var; Sentry .NET ile hata izleme.
- Çalışma süresi izleme (UptimeRobot, Better Stack) ve Google kotası %80'e gelince uyarı.
- Kabul: kasıtlı bir hata hem istemcide hem sunucuda panoda görünüyor; hiçbir olayda e-posta ya da konum yok.

---

### FAZ 3 — Ürün kalitesi

**3.1 Hesap akışları**
- **Şifremi unuttum** yok. E-posta ile tek kullanımlık, süreli sıfırlama bağlantısı (ASP.NET Identity token
  sağlayıcıları zaten kayıtlı). E-posta için bir sağlayıcı (Postmark, Resend, Amazon SES).
- E-posta doğrulama: doğrulanmamış hesap yorum ve fotoğraf yükleyemesin.
- İsteğe bağlı ama önerilen: "Google ile giriş" (Credential Manager). v1 için zorunlu değil.
- Kabul: sıfırlama ve doğrulama akışları API testleriyle kapsanıyor; e-posta şablonları Türkçe.

**3.2 Paylaşım ve derin bağlantılar**
- Paylaşım şu an OSM bağlantısı gönderiyor. `https://nereye.app/m/{placeId}` gibi bir web adresi üret;
  Android App Links (`assetlinks.json`) ile uygulama yüklüyse uygulamada, değilse basit bir web sayfasında açılsın.
- Kabul: `adb shell am start -d https://nereye.app/m/...` doğrudan mekan sayfasını açıyor.

**3.3 Performans ve kararlılık**
- Play Android Vitals eşikleri: kullanıcı başına çökme ve ANR oranları. Soğuk açılışı ölç ve hedef koy
  (orta seviye Android'de 2 sn altı).
- Liste ekranında `FlatList` ayarları var; ana sayfa `ScrollView` içinde çok sayıda kart çiziyor, ölç ve
  gerekirse sanallaştır.
- Görseller: `expo-image` önbelleği var; kart için küçük, detay için büyük boyut iste.
- Ağ yokken anlamlı ekran: son başarılı listeyi göster ve "çevrimdışı" bandı koy.
- Kabul: düşük donanımlı bir emülatörde (2 GB RAM) ana akışlarda donma yok; ölçümler `docs/performans.md` içinde.

**3.4 Erişilebilirlik ve görünüm**
- TalkBack ile tüm akışları gez; her dokunulabilir öğenin etiketi var mı kontrol et. Yazı boyutu %200'de
  düzen kırılmamalı.
- Koyu tema (`userInterfaceStyle: light` sabit): v1 için zorunlu değil, ama sistem koyu modda durum çubuğu ve
  alt çubuk okunabilir olmalı.
- Android geri hareketi (`predictiveBackGestureEnabled: false`): Android 15+ davranışını test et.

**3.5 Temizlik**
- `apps/mobile` (Flutter arşivi) repoyu ve CI'ı şişiriyor: ayrı bir arşiv dalına taşı ve ana daldan sil.
- `main` dalı bu daldan ayrışmış durumda (`b8863aa`). Bu dalı `main`'e PR ile birleştir; çakışmalarda bu
  daldaki sürüm esastır, `main`'deki `map.web.tsx` / `RouteMap` gibi parçaları tek tek değerlendir.

---

### FAZ 4 — Test, CI/CD, sürüm yönetimi

**4.1 Uçtan uca testler**
- Android emülatörde Maestro akışları: ilk açılış ve onboarding, konum izni reddi, niyet seçimi ve liste,
  mekan detayı, kayıt / giriş / çıkış, yorum yazma / düzenleme / silme, favori, hesap silme.
- Niyet doğruluğu için ayrı bir akış: her niyet sekmesinde ilk 10 kartın kategorisi o niyete ait mi.
- Kabul: akışlar CI'da emülatörde koşuyor.

**4.2 CI (GitHub Actions)**
- Her PR: `dotnet test`, `npm test`, `npm run typecheck`, `npm ci` (lockfile senkronu), lint.
- `main`'e birleşince: Docker imajları, migration bundle, staging'e dağıtım.
- Etiket (`v1.0.0`): `eas build --profile production --platform android` ve `eas submit` (Play iç test kanalı).
- Kabul: kırmızı CI ile birleştirme yapılamıyor (branch protection).

**4.3 Sürümleme ve güncellemeler**
- `eas.json` `appVersionSource: "local"` ve `autoIncrement: false`: `versionCode` elle artırılıyor ve
  unutulursa yükleme reddedilir. `appVersionSource: "remote"` ve `autoIncrement: true` yap.
- `expo-updates` ile OTA güncelleme: `runtimeVersion` politikası `appVersion`; yalnızca JS düzeltmeleri OTA
  ile, yerel değişiklik içeren her sürüm mağazadan.
- Play App Signing kullan; yükleme anahtarını EAS yönetsin.
- Kabul: art arda iki production derlemesi farklı `versionCode` alıyor.

---

### FAZ 5 — Mağaza girişi

- **Mağaza kaydı (Türkçe):** uygulama adı (30), kısa açıklama (80), tam açıklama (4000, anahtar kelime
  doldurma yok), uygulama ikonu 512×512, özellik görseli 1024×500, en az 4 telefon ekran görüntüsü
  (gerçek uygulamadan, Maestro ile otomatik al). `store-assets-wip` etiketindeki taslakları kullan ya da yenile.
- **İçerik derecelendirmesi anketi:** kullanıcı içeriği ve konum paylaşımı "evet"; şiddet vb. "hayır".
- **Hedef kitle:** 18+ (ya da 13+). Çocuklara yönelik değil; "Ailelere yönelik" programına girme.
- **Reklam beyanı:** sponsorlu kartlar varsa "Reklam içerir: evet".
- **Uygulama erişimi:** inceleme ekibi için test hesabı ve giriş adımları.
- **Veri güvenliği formu:** `docs/play-veri-guvenligi.md` dosyasından.
- **Hesap silme adresi** ve **gizlilik politikası adresi:** Faz 0.7 ve 1.1'den.
- Bütün metinleri `docs/play-magaza-kaydi.md` dosyasında hazırla; insan Play Console'a kopyalayacak.

---

### FAZ 6 — Yayın ve sonrası

1. İç test → kapalı test → üretim (aşamalı: %10 → %50 → %100).
2. Kapalı testte Play'in yeni kişisel geliştirici hesapları için istediği asgari test kullanıcısı ve süre
   şartını doğrula (2024'ten beri 12 kullanıcı ve 14 gün) ve karşıla.
3. İlk 72 saat: Android Vitals, Sentry, sunucu hataları ve Google kotası her gün kontrol; eşik aşılırsa
   aşamalı dağıtımı durdur.
4. Mağaza yorumlarına yanıt şablonları.

---

### Alınmış kararlar (bunları sorma)

- Platform: yalnızca Android, v1. iOS sonra.
- Harita: Android'de Google Maps sağlayıcısı (0.3-A).
- Ödeme: v1'de uygulama içi satın alma yok (0.6).
- Çökme raporlama: Sentry. E-posta: Resend ya da Postmark (hangisinin entegrasyonu daha basitse).
- Dil: yalnızca Türkçe. Tema: yalnızca açık.
- Giriş: e-posta + şifre; Google ile giriş v1.1.
- Flutter arşivi ana daldan çıkar.
- Belirsiz bir politika maddesinde: daha kısıtlayıcı yorumu uygula ve riski kaydet.

### Tamamlanma tanımı

- Faz 0–4'ün tüm kabul kriterleri karşılandı; Faz 5 metinleri hazır.
- `docs/yayin-insan-gorevleri.md` dosyasında yalnızca gerçekten insan gerektiren işler kaldı.
- `docs/yayin-riskleri.md` dosyasında "doğrulanmadı" durumunda madde yok ya da her birinin sahibi belli.
- Production AAB iç test kanalına yüklendi ve gerçek bir Android cihazda uçtan uca denendi.

---

## İnsanın yapması gerekenler (ajan bunları yapamaz)

Ajan bu listeyi `docs/yayin-insan-gorevleri.md` dosyasına taşıyıp güncelleyecek. Başlangıç hâli:

1. **Google Play geliştirici hesabı** (tek seferlik ücret), kimlik doğrulaması ve D-U-N-S
   (kuruluş hesabıysa).
2. **Alan adı** (`nereye.app` ya da başka) ve DNS erişimi.
3. **Barındırma ve yönetilen veritabanı** hesabı, ödeme yöntemi.
4. **Google Cloud faturalandırma**: Places kotalarını yükseltme, bütçe uyarısı, sızan anahtarı yenileme.
5. **Harita sağlayıcısı / Google Maps Android anahtarı**, e-posta sağlayıcısı, Sentry hesabı.
6. **Hukuk**: KVKK aydınlatma metni, açık rıza metinleri, kullanım koşulları, yurt dışı aktarım değerlendirmesi,
   VERBİS kayıt yükümlülüğü olup olmadığı. Bunları bir avukat onaylamalı; ajanın yazdığı metin taslaktır.
7. **Kapalı test için test kullanıcıları** (ör. 12 kişi, 14 gün).
8. **Mağaza görselleri** için son onay ve marka kararları.
