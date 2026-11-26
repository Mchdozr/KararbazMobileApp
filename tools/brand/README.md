# Kararbaz logosu

Logo kodla çizilir: `mark.html` (SVG + Fraunces Black), `render.cjs` ile PNG'ye dökülür. Yazı tipi
`apps/nereye-go/node_modules` içinden okunur; önce `apps/nereye-go` içinde `npm ci` çalıştırın.

```bash
cd e2e    # Playwright burada kurulu
node ../tools/brand/render.cjs "$PWD/../tools/brand" /tmp/logo.png "bg=1&scale=0.86"
```

Parametreler:

| Parametre | Anlamı |
| --- | --- |
| `bg=1` | Krem zemin (`#f4dfbf`). Yoksa saydam. |
| `scale` | İşaretin tuvale oranı. Uygulama simgesi 0.80, logo/açılış 0.86, Android ön plan ve tek renk 0.58. |
| `mono=1` | Beyaz tek renk (Android tematik simge). Konum işaretinin içi saydam yapılmalı (siyahı alfaya çevir). |

Uygulamadaki dosyalar: `apps/nereye-go/assets/images/` (icon, logo, splash-icon, android-icon-*, favicon).
