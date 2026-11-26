# Play mağaza ekran görüntüleri

Gerçek veriyle (OSM + Google zenginleştirmesi, gerçek rota ve hava) 8 telefon görseli üretir. Çıktı:
`docs/tanitim/magaza/`. Ayrıntı ve dikkat edilecekler: `docs/play-magaza-kaydi.md` ("Mağaza görselleri").

1. Kendi Overpass/OSRM (İstanbul kırpımı): `docker compose -f docker-compose.yml -f docker-compose.selfhost.yml up -d
   osrm-foot osrm-car overpass`. Konteynerler internete proxy üzerinden çıkıyorsa
   `selfhost-proxy.override.example.yml` örneğindeki gibi bir ek dosya kullanılır (proxy portu ortama göre değişir).
2. Gerçek veri API'si (5098): `Places__UseFake=false`, `GOOGLE_PLACES_API_KEY` (sır: yalnızca ortam değişkeni),
   `Overpass__Endpoints__0=http://localhost:12345/api/interpreter`, `Routing__FootBaseUrl=http://localhost:5001/`,
   `Routing__CarBaseUrl=http://localhost:5002/`, bellek içi veritabanı.
3. Web derlemesi: `EXPO_PUBLIC_API_URL=http://localhost:5098 npx expo export -p web --output-dir /tmp/kararbaz-web`,
   `npx serve -s -l 8097 /tmp/kararbaz-web`.
4. Fotoğrafları önbelleğe al: `python3 warm.py http://localhost:5098 12` (Google Text Search günlük kotası sınırlı;
   yalnızca ekranda görünen mekanlar).
5. Kayıt ve çerçeve (e2e klasöründen, Playwright orada kurulu):
   `node ../tools/store-screenshots/capture.cjs "$PWD/../tools/store-screenshots/raw"` ve
   `node ../tools/store-screenshots/render.cjs "$PWD/../tools/store-screenshots"` → `out/`.

Zincir marka logosu (Starbucks) görünmesin diye o kayıt çekimde listeden çıkarılır; veri uydurulmaz.
