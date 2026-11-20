# Yayın için insan görevleri

Bunları kod yapamaz ya da yapmamalıdır: hesap açmak, para ödemek, sır üretmek, hukuki onay vermek, gerçek
kullanıcı bulmak. **Hiçbiri yapılmış sayılmaz**; her kutu bilerek boş bırakıldı. Yapan kişi işaretler.

## Hesaplar ve altyapı

- [ ] **Google Play geliştirici hesabı** açmak / doğrulamak (kimlik ve ödeme Google'a insan tarafından verilir).
- [ ] **Alan adı** almak ve DNS'i yönetmek. Repoda hiçbir alan adı gerçek değildir; `docs/play-store-hazirlik.md`
      içindeki adresler örnektir.
- [ ] **Barındırma:** API (`src/Nereye.Api`), Postgres ve Redis için sunucu; https sertifikası; ters vekil.
      Ters vekil arkasında `Proxy__TrustForwardedHeaders=true`.
- [ ] **Yönetim paneli** (`src/Nereye.Admin`) için ayrı host, VPN ya da IP kısıtı kurmak. Panel herkese açık
      internete konmaz (README → Prod notları).
- [ ] Her dağıtımda şemayı ayrı adımla güncellemek: `dotnet Nereye.Api.dll migrate`
      (`docs/veritabani-guncelleme.md`). `TimestampTz` migration'ından önce **veritabanı yedeği** almak.

## Sırlar ve ortam değişkenleri (repoya yazılmaz)

- [ ] `JWT_SIGNING_KEY` — en az 32 karakter, rastgele. Üretimi ve saklaması insan işidir.
- [ ] `ADMIN_PASSWORD` — verilmezse Production'da admin hesabı oluşmaz. Tohumlanan admin hesabının giriş adı
      eskiden kalma `admin@nereye.app`'tir (`src/Nereye.Infrastructure/Persistence/NereyeSeeder.cs`); bu bir giriş
      kimliğidir, oraya e-posta gönderilmez. Gerçek bir adresle değiştirmek insan kararıdır.
- [ ] **Google faturalandırma** hesabını açmak; Places API (New) ve Maps SDK for Android'i etkinleştirmek; günlük
      kotaları ayarlamak.
- [ ] **Sızmış Google anahtarını yenilemek.** Bu projede bir Places anahtarı sohbet içinde açık metin olarak
      paylaşıldı; o anahtar Google Cloud Console'da **insan tarafından silinip yenisi üretilmeli** ve yalnızca
      ortam değişkeniyle (`GOOGLE_PLACES_API_KEY`) verilmeli. Yeni anahtarı API ve IP ile kısıtla.
- [ ] **Harita anahtarı:** Android için ayrı bir Maps SDK anahtarı; paket adı `com.mchdozr.nereye` ve imza SHA-1
      ile kısıtlı. Derlemede `GOOGLE_MAPS_ANDROID_API_KEY` ortam değişkeni (EAS secret). Yoksa production
      derlemesi durur.
- [ ] İsteğe bağlı iOS harita anahtarı (Maps SDK for iOS, paket kimliğiyle kısıtlı): `GOOGLE_MAPS_IOS_API_KEY`.
      Verilmezse iPhone'da Apple Haritalar kullanılır.
- [ ] **E-posta sağlayıcısı** (Resend ya da Postmark) hesabı, gönderici alan adı doğrulaması (SPF/DKIM) ve:
      `Email__Provider` (`resend`|`postmark`), `Email__ApiKey`, `Email__From`.
      Yoksa e-posta doğrulama, şifre sıfırlama ve web'den hesap silme kapalı kalır ve **kimse yorum yazamaz**.
- [ ] `Api__PublicBaseUrl` — API'nin https kökü (e-postadaki bağlantılar bundan kurulur).
- [ ] `EXPO_PUBLIC_API_URL` — production derlemesinde API'nin https adresi. Yoksa ya da https değilse derleme durur.
- [ ] `Places__Contact` (ya da `NEREYE_CONTACT`) — OSM servislerine gönderilen User-Agent iletişim bilgisi.
- [ ] `Legal__Contact` — gizlilik metnindeki iletişim adresi. Yoksa metin "İletişim adresi tanımlanmadı" der.
- [ ] `Cors__Origins` — yalnızca tarayıcıdan erişecek web kökenleri (mobil uygulama CORS kullanmaz).
- [ ] İsteğe bağlı: `EXPO_PUBLIC_PROMOTION_CONTACT_URL` (https ya da mailto; "bize ulaşın" düğmesi),
      `EXPO_PUBLIC_SHARE_BASE_URL` (https; paylaşıma web bağlantısı ekler, App Links kurulumu da insan işi).

## Play Console

- [ ] **Hesap silme adresi:** İnsan Play Console'a kendi API hostunu yapıştırır: `https://{API hostu}/hesap-sil`.
      Sayfa e-posta sağlayıcısı ve `Api__PublicBaseUrl` olmadan 503 verir.
- [ ] **Gizlilik politikası adresi:** `https://{API hostu}/gizlilik` (koşullar: `/kosullar`).
- [ ] **KVKK / gizlilik metninin avukat onayı** (`src/Nereye.Api/LegalTexts.cs` taslaktır).
- [ ] Veri güvenliği formunu `docs/play-veri-guvenligi.md` ile doldurmak; mağaza kaydını
      `docs/play-magaza-kaydi.md` ile hazırlamak.
- [ ] **Kapalı test: 12 gerçek kullanıcı, 14 gün** (yeni kişisel hesaplar için Play şartı; güncel sayıyı Play
      Console'da doğrula). Testçiler uydurulmaz; gerçek kişiler davet edilir.
- [ ] **Mağaza görselleri** (ikon, öne çıkan grafik, ekran görüntüleri) ve metinlerinin onayı.
- [ ] Hedef API seviyesi şartını Play Console'da doğrulamak (kodda `targetSdkVersion` 36).

## Sürüm numarası

- [ ] `apps/nereye-go/eas.json` şu an `appVersionSource: local`, `autoIncrement: false`; `app.json` sürüm
      1.0.1 / versionCode 3. **İnsan EAS'ta remote sürüm kaynağını ve autoIncrement'i açar** (`eas build:version:set`
      ile mevcut versionCode'u girip `appVersionSource: remote`, `autoIncrement: true`). Kod bunu yapmadı;
      versionCode elle artırılmadı.

## Kod incelemesi

- [ ] `origin/main` üzerindeki `b8863aa` ("Harita/rota ve mağaza görselleri") bu dalda yok: ayrık commit, elle
      bakılıp birleştirilecek.
