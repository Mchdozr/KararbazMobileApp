# Hava durumuna göre öneri

## Ne yapıyor

- Öneri isteğiyle paralel olarak anlık hava istenir. Bölge yaklaşık 5 km'dir (geohash 5), sonuç 20 dakika önbellekte
  kalır. Hava 600 ms içinde gelmezse liste havasız döner; kullanıcı hiç beklemez.
- **Yağış** (yağmur, kar, fırtına): açık hava mekanlarının (park, bahçe…) puanı ×0,55. Kapalı mekanlara dokunulmaz.
- **Güzel hava** (açık ya da parçalı bulutlu, 15–29°, gündüz):
  - açık hava ×1,15
  - dış masası olan mekan ×1,08
- **Soğuk** (5° altı): açık hava ×0,8.
- Ana sayfada Türkçe bir not gösterilir, gerekiyorsa niyet önerisiyle birlikte:
  - yağmurda Açık hava seçiliyse "Kültür"e geçiş önerilir;
  - güzel havada "Açık hava"ya bakma önerilir.

Kurallar: `src/Nereye.Domain/Weather/WeatherRules.cs` (testli).

## Sağlayıcı ve lisans

Varsayılan sağlayıcı [Open-Meteo](https://open-meteo.com):

- **Ücretsiz uç noktası** (`api.open-meteo.com`) anahtar istemez, ama **yalnızca ticari olmayan kullanım** içindir.
- **Ticari kullanım** için ücretli abonelik gerekir: `customer-api.open-meteo.com` ve bir API anahtarı.

Bu yüzden:

| Ortam | Durum |
| --- | --- |
| Yayın (`appsettings.json`) | `Weather:Enabled = false`. Açmak insan kararıdır. |
| docker-compose (geliştirme) | Açık |
| Sahte veri modu (`Places:UseFake`) | Gerçek istek yok, sahte hava |

Yayında açmak için ortam değişkenleri:

```text
Weather__Enabled=true
Weather__BaseUrl=https://customer-api.open-meteo.com/   # ücretli abonelikte
Weather__ApiKey=...                                     # sır: ortam değişkeni ya da user-secrets, repoya değil
```

Sahte modda hava `Weather:Fake` ile seçilir: `clear`, `cloudy`, `rain`, `snow`, `storm` ya da `fog`. Örneğin
`Weather__Fake=rain` ile yağmurlu davranış denenir.

Ölçümler panelinde servis adı `open-meteo` olarak görünür.
