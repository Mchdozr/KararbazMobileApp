const { chromium } = require('playwright');
const S = process.argv[2];
const items = [
  ['01-ana-sayfa', 'Canın ne<br>çekiyorsa <span class="a">seç</span>'],
  ['02-liste', 'Yakınındaki yerler,<br><span class="a">kısa bir listede</span>'],
  ['03-filtre', 'Açık mı, sessiz mi?<br><span class="a">Filtrele</span>'],
  ['04-benim-icin-sec', 'Kararsız mısın?<br><span class="a">Kararbaz seçsin</span>'],
  ['05-oylama', 'Arkadaşlarınla oyla,<br><span class="a">kazanan yere git</span>'],
  ['06-mekan', 'Saatler, yol tarifi<br><span class="a">tek sayfada</span>'],
  ['07-listelerim', 'Listeni yap,<br><span class="a">paylaş</span>'],
  ['08-gece-modu', 'Gece modu<br><span class="a">da hazır</span>'],
];
(async () => {
  const b = await chromium.launch({ args: ['--allow-file-access-from-files'] });
  const p = await b.newPage({ viewport: { width: 1080, height: 1920 }, deviceScaleFactor: 1 });
  for (const [name, h] of items) {
    const qs = new URLSearchParams({ h, img: `raw/${name}.png` });
    await p.goto(`file://${S}/frame.html?${qs}`); await p.waitForFunction(() => window.done); await p.waitForTimeout(150);
    await p.screenshot({ path: `${S}/out/kararbaz-${name}.png` }); console.log(name);
  }
  await b.close();
})();
