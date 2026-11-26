# Bildirimler

## Ne gönderiliyor

Şu an tek tür var: **etkinlik hatırlatması**. Kullanıcının kaydettiği (favori) bir mekanda etkinlik başlamak üzereyse
bildirim gider. Örnek: "Bu akşam: Akustik gece · MiniMüzikhol · 20:30'da başlıyor". Kurallar
(`src/Nereye.Domain/Push/PushRules.cs`, testli):

- Etkinliğe 30 dk ile 3 sa arası kala gönderilir.
- Etkinlik ve cihaz başına bir kez gönderilir.
- Cihaz başına günde en fazla 3 bildirim gider.
- Bildirime dokununca mekan sayfası açılır. Yalnızca uygulamanın kendi `nereyego://` bağlantıları kabul edilir
  (`lib/push-route.ts`).

"Kaydettiğin mekan şu an açık ve 5 dk uzakta" türü bildirim **yapılmadı**: arka planda konum izlemeyi gerektirir. Bu da
ayrı bir rıza, mağaza beyanı ve pil yükü demektir.

## Rıza

- Varsayılan kapalıdır.
- Kullanıcı Profil'de "Etkinlik hatırlatmaları"nı açınca sistem izni istenir. Ardından Expo belirteci
  `POST /api/me/push` ile kaydedilir ve rıza günlüğüne `notifications` yazılır.
- Kapatınca (`DELETE /api/me/push`) belirteç ve gönderim kayıtları silinir.
- Hesap silinince belirteç ve gönderim kayıtları da silinir.
- Gizlilik metninde "Bildirimler" bölümü vardır.

## Sunucu ayarları

| Ayar | Varsayılan | Ne |
| --- | --- | --- |
| `Push:Enabled` | `false` | Açılınca hatırlatma işi 15 dk'da bir çalışır ve Expo'ya gönderir. Kapalıyken hiçbir şey gönderilmez. |
| `Push:AccessToken` | — | Expo hesabında "enhanced push security" açıksa erişim anahtarı. Sırdır: ortam değişkeni ya da user-secrets ile verilir. |
| `Push:Endpoint` | `https://exp.host/--/api/v2/push/send` | Expo push servisi |

Geçersiz belirteçler (`DeviceNotRegistered`) otomatik silinir. Ölçümler panelinde servis adı `expo-push` olarak görünür.

## Durum

- **Doğrulanan:**
  - Sunucu tarafı uçtan uca: sahte gönderici ile API testleri.
  - Expo yanıtının ayrıştırılması: 100'lük paketler, geçersiz belirteç.
  - Bildirim bağlantısının ekrana çevrilmesi.
  - **Gerçek Expo servisiyle** (`LiveExpoPushTests`, `NEREYE_LIVE_EXPO=1`, 2026-11-26): uydurma belirteçlere
    gönderim `DeviceNotRegistered` döndü. Hatırlatma işi gerçek servis üzerinden çalıştı ve geçersiz belirteci
    veritabanından sildi. Hiçbir cihaza bildirim gitmedi.

    ```bash
    NEREYE_LIVE_EXPO=1 dotnet test tests/Nereye.Api.Tests --filter LiveExpoPushTests
    ```
- **Doğrulanmayan:** gerçek cihazda izin, belirteç ve bildirimin görünmesi. Bu ortamda cihaz, emülatör (KVM yok)
  ve EAS derlemesi yok.
  - Push belirteci yalnızca geliştirme ya da yayın derlemesinde alınır; Expo Go'da alınmaz.
  - `app.json` içine `expo-notifications` eklentisi eklendi.
  - Android'de FCM için `google-services.json` gerekir. Bu dosya insan tarafından eklenir ve repoya konmaz.
