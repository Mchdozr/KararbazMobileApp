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

Varsayılan sağlayıcı **MET Norway** (Norveç Meteoroloji Enstitüsü, `api.met.no`, Locationforecast 2.0):

- **Ücretsiz ve ticari kullanım serbest.** Veri CC BY 4.0 lisanslıdır; atıf gerekir.
  - Uygulamada hava notunun altında "Kaynak: MET Norway" yazar.
  - Kullanım koşullarında lisans bağlantısı var; gizlilik metninde MET'e ne gittiği yazılı.
- **Kurallarına uyum** (`MetNorwayWeatherProvider`, testli):
  - Tanımlayıcı User-Agent: `HttpIdentity` ile `Nereye/1.0 (Places:Contact)`. Ya da `Weather:UserAgent` ile açıkça
    verilir. MET iletişim bilgisi ister; yayında `Places__Contact` (ya da `NEREYE_CONTACT`) doldurulmalıdır. Bu
    ayar OSM servisleri için zaten gerekli.
  - Koordinat en çok 4 ondalık. Gönderilen nokta ~5 km'lik bölgenin (geohash 5) merkezidir; kullanıcının tam
    konumu ve IP adresi MET'e gitmez.
  - Aynı bölge `Expires` başlığından önce, ve Kuzey Avrupa dışı için kuralı gereği 3 saatten sık sorulmaz
    (en çok 12 saat). Süre dolunca `If-Modified-Since` gönderilir; 304'te eski seri kullanılır.
  - gzip ve yönlendirme açık. Aynı bölge için eşzamanlı istekler tek çağrıya iner.
  - Hata olursa eski seri kullanılır ve bölge 10 dakika, 429/403'te 1 saat sorulmaz.
  - 203 (sürüm kalkıyor) yanıtı çalışmaya devam eder ve bir kez uyarı günlüğe düşer.
- Saatlik tahmin serisi önbellekte durur. "Şu an" her istekte seriden okunur. Gündüz/gece güneşin konumundan
  hesaplanır (`SunPosition`).

Open-Meteo seçenek olarak duruyor (`Weather:Provider=OpenMeteo`). Ücretsiz ucu ticari olmayan kullanım içindir;
yalnızca ücretli abonelikle kullanılmalıdır.

| Ortam | Durum |
| --- | --- |
| Yayın (`appsettings.json`) | Açık: `Weather:Enabled=true`, `Weather:Provider=MetNorway` |
| docker-compose (geliştirme) | Açık |
| Sahte veri modu (`Places:UseFake`) | Sahte hava, kaynak "demo". `Weather__Fake=live` ile örnek mekanlar gerçek MET verisiyle denenir. |

Ayarlar:

```text
Weather__Enabled=true
Weather__Provider=MetNorway            # ya da OpenMeteo (ücretli: Weather__BaseUrl + Weather__ApiKey)
Weather__UserAgent=                    # boşsa Nereye/1.0 (Places__Contact)
```

Sahte modda hava `Weather:Fake` ile seçilir: `clear`, `cloudy`, `rain`, `snow`, `storm`, `fog` ya da `live`.
Örneğin `Weather__Fake=rain` ile yağmurlu davranış denenir.

Ölçümler panelinde servis adı `met-norway` (ya da `open-meteo`) olarak görünür.

## Doğrulama

- Birim testleri:
  - simge kodu eşlemesi, seri ayrıştırma, gündüz/gece (`MetForecastTests`);
  - 3 saat kuralı, `Expires`, `If-Modified-Since`/304, hata sonrası bekleme, eşzamanlı istekler (`MetNorwayTests`).
- Canlı deneme (2026-11-26): API sahte mekan + `Weather__Fake=live` ile çalıştırıldı ve gerçek `api.met.no`
  yanıtı alındı.
  - Kadıköy ve Beşiktaş için toplam 2 istek gitti; aynı bölgeden üçüncü istek önbellekten döndü.
  - Yanıt: `{"kind":"Cloudy","temperatureC":8,"source":"MET Norway"}`.
  - İlk istek 839 ms sürdü (soğuk bağlantı). 600 ms bütçesini aştığı için o liste havasız döndü, önbellek arkada
    doldu. Sonraki istek 171 ms.
