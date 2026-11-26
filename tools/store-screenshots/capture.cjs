// 30 sn v5: akşam saati, girişli demo hesap; hava notu, Bu akşam, Benim için seç, oylama, mekan, listeler, karanlık tema.
// Çıktı: <out>/fNNNNN.jpg + take.json {times, marks, taps, boxes}
const { chromium } = require('playwright');
const fs = require('fs');
const out = process.argv[2];
const BASE = 'http://localhost:8097';
const API = 'http://localhost:5098';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const ease = (p) => (p < 0.5 ? 4 * p * p * p : 1 - Math.pow(-2 * p + 2, 3) / 2);

// Yerel saati 19-20 arası olan bir saat dilimi seç (uygulama "İyi akşamlar" der, akşam degradesi)
function eveningZone() {
  const zones = ['Pacific/Kiritimati', 'Pacific/Auckland', 'Pacific/Fiji', 'Asia/Magadan', 'Australia/Sydney', 'Asia/Tokyo', 'Asia/Shanghai',
    'Asia/Bangkok', 'Asia/Dhaka', 'Asia/Karachi', 'Asia/Dubai', 'Europe/Istanbul', 'Europe/Berlin', 'Europe/London', 'Atlantic/Azores',
    'America/Noronha', 'America/Sao_Paulo', 'America/New_York', 'America/Chicago', 'America/Denver', 'America/Los_Angeles', 'America/Anchorage', 'Pacific/Honolulu'];
  for (const z of zones) {
    const h = +new Intl.DateTimeFormat('en-GB', { timeZone: z, hour: '2-digit', hour12: false }).format(new Date());
    const m = +new Intl.DateTimeFormat('en-GB', { timeZone: z, minute: '2-digit' }).format(new Date());
    if ((h === 19 && m >= 5) || (h === 20 && m < 40)) return z;
  }
  return 'Europe/Istanbul';
}

(async () => {
  const zone = eveningZone();
  console.log('saat dilimi', zone);
  const email = `deniz.${Date.now()}@example.com`, password = 'Nereye.Demo2026!';
  const reg = await fetch(API + '/api/auth/register', { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, displayName: 'Deniz', acceptPrivacy: true, acceptTerms: true }) });
  console.log('kayıt', reg.status);

  const browser = await chromium.launch({ args: ['--force-device-scale-factor=3', '--hide-scrollbars'] });
  const ctx = await browser.newContext({ viewport: { width: 393, height: 852 }, deviceScaleFactor: 3, timezoneId: zone, locale: 'tr-TR' });
  await ctx.addInitScript(() => {
    localStorage.setItem('nereye.onboardedAt', new Date().toISOString()); localStorage.setItem('analytics.consent', 'denied');
    const st = document.createElement('style');
    st.textContent = '*::-webkit-scrollbar{display:none!important} *{scrollbar-width:none!important; caret-color: transparent !important} input:focus{outline:none!important}';
    document.addEventListener('DOMContentLoaded', () => document.head.appendChild(st));
    new MutationObserver(() => {
      document.querySelectorAll('div').forEach((el) => {
        if (el.childElementCount === 0 && (el.textContent === 'Web önizlemesi · telefonda gerçek harita' || el.textContent === 'Kaynak: demo')) el.style.visibility = 'hidden';
      });
    }).observe(document, { childList: true, subtree: true });
  });
  await ctx.route(/\/_expo\/static\/js\/web\/entry-.*\.js$/, async (route) => {
    const res = await route.fetch();
    const body = (await res.text()).replace('Object.freeze({top:0,right:0,bottom:0,left:0})', 'Object.freeze({top:59,right:0,bottom:34,left:0})');
    await route.fulfill({ response: res, body });
  });
  await ctx.route(/localhost:5098\/api\//, async (route) => {
    const res = await route.fetch();
    const ct = res.headers()['content-type'] || '';
    if (!ct.includes('json')) return route.fulfill({ response: res });
    let txt = await res.text();
    // Mağaza görseli: tescilli marka logosu görünmesin diye zincir marka kaydı listeden çıkarılır (veri uydurulmaz).
    if (/\/api\/(recommendations|search)/.test(route.request().url())) {
      try { const j = JSON.parse(txt); if (Array.isArray(j.items)) j.items = j.items.filter((i) => !/starbucks/i.test(i.name || '')); txt = JSON.stringify(j); } catch {}
    }
    // Demo etkinliği öğlene kurulu: akşam sahnesinde bu akşam 21:00'e alınır (yalnızca kayıt).
    if (route.request().url().includes('/api/events/tonight')) {
      try { const j = JSON.parse(txt); const d = new Date(); const day = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul' }).format(d);
        for (const e of j.items || []) { e.startsAt = new Date(day + 'T18:00:00Z').toISOString(); e.endsAt = new Date(day + 'T20:00:00Z').toISOString(); e.live = false; }
        txt = JSON.stringify(j); } catch {}
    }
    if (txt.includes('nereye:mini')) {
      const fix = (o) => { if (Array.isArray(o)) o.forEach(fix); else if (o && typeof o === 'object') {
        const id = o.id || o.placeId || o.venueId;
        if (typeof id === 'string' && id.includes('nereye:mini')) for (const k of Object.keys(o)) if (/^open(Now)?$/i.test(k) && o[k] === false) o[k] = true;
        Object.values(o).forEach(fix); } };
      try { const j = JSON.parse(txt); fix(j); txt = JSON.stringify(j); } catch {}
    }
    await route.fulfill({ response: res, body: txt });
  });
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Emulation.setSafeAreaInsetsOverride', { insets: { top: 59, bottom: 34, left: 0, right: 0 } });

  const settle = async (ms = 900) => {
    await page.waitForLoadState('networkidle').catch(() => {});
    await page.evaluate(async () => { for (const i of document.images) if (!i.complete) await new Promise((r) => { i.onload = i.onerror = r; setTimeout(r, 3000); }); });
    await sleep(ms);
  };
  const raf = () => page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const rect = (sel, text) => page.evaluate(({ sel, text }) => {
    const all = [...document.querySelectorAll(sel)].filter((el) => (text ? el.textContent.trim() === text : true));
    for (const el of all) {
      if (!el.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true })) continue;
      const r = el.getBoundingClientRect();
      if (r.width < 2 || r.height < 2 || r.bottom < 0 || r.top > innerHeight) continue;
      const hit = document.elementFromPoint(r.left + r.width / 2, r.top + Math.min(r.height / 2, 14));
      if (hit && (el.contains(hit) || hit.contains(el))) return { x: r.left, y: r.top, w: r.width, h: r.height };
    }
    return null;
  }, { sel, text });
  const markScroller = (x, y) => page.evaluate(({ x, y }) => {
    document.querySelectorAll('[data-rec-scroll]').forEach((e) => e.removeAttribute('data-rec-scroll'));
    let el = document.elementFromPoint(x, y);
    while (el && !(el.scrollHeight > el.clientHeight + 4 && /(auto|scroll)/.test(getComputedStyle(el).overflowY))) el = el.parentElement;
    if (!el) return null;
    el.setAttribute('data-rec-scroll', '1');
    return true;
  }, { x, y });
  const markHScroller = (x, y) => page.evaluate(({ x, y }) => {
    document.querySelectorAll('[data-rec-h]').forEach((e) => e.removeAttribute('data-rec-h'));
    let el = document.elementFromPoint(x, y);
    while (el && !(el.scrollWidth > el.clientWidth + 4 && /(auto|scroll)/.test(getComputedStyle(el).overflowX))) el = el.parentElement;
    if (!el) return null;
    el.setAttribute('data-rec-h', '1');
    return { left: el.scrollLeft, max: el.scrollWidth - el.clientWidth };
  }, { x, y });
  const setH = (v) => page.evaluate((v) => { const el = document.querySelector('[data-rec-h]'); el.scrollLeft = v; el.dispatchEvent(new Event('scroll')); }, v);
  const hSeq = async (to, t0, dur, fps = 60) => {
    const s0 = await page.evaluate(() => document.querySelector('[data-rec-h]').scrollLeft);
    const N = Math.round(dur * fps);
    for (let i = 1; i <= N; i++) { await setH(s0 + (to - s0) * ease(i / N)); await shot(t0 + i / fps); }
  };
  const setScroll = (v) => page.evaluate((v) => { const el = document.querySelector('[data-rec-scroll]'); el.scrollTop = v; el.dispatchEvent(new Event('scroll')); }, v);

  const log = { zone, times: [], marks: {}, taps: [], boxes: {} };
  let n = 0;
  const shot = async (t) => {
    await raf();
    await page.screenshot({ path: `${out}/f${String(n).padStart(5, '0')}.jpg`, type: 'jpeg', quality: 94 });
    log.times.push(+t.toFixed(4)); n++;
  };
  const box = async (name, sel, text) => { log.boxes[name] = await rect(sel, text); if (!log.boxes[name]) console.log('kutu yok', name); return log.boxes[name]; };
  const scrollSeq = async (dy, t0, dur, fps = 60) => {
    const s0 = await page.evaluate(() => document.querySelector('[data-rec-scroll]').scrollTop);
    const N = Math.round(dur * fps);
    for (let i = 1; i <= N; i++) { await setScroll(s0 + dy * ease(i / N)); await shot(t0 + i / fps); }
  };
  // Basılı kare t'de; bırakınca uygulama tepki verir
  const press = async (b, t, label, fy = 0.5) => {
    if (!b) throw new Error('kutu yok: ' + label);
    const x = b.x + b.w / 2, y = b.y + b.h * fy;
    await page.mouse.move(x, y); await page.mouse.down(); await sleep(120);
    await shot(t); log.taps.push({ t, x, y, label });
    await page.mouse.up();
  };

  const snap = async (name) => { await raf(); await page.screenshot({ path: `${out}/${name}.png` }); console.log('ekran', name); };
  const tap = async (sel, text) => { const l = text ? page.locator(sel).filter({ hasText: text }) : page.locator(sel); await l.filter({ visible: true }).first().click(); };
  await page.goto(BASE + '/auth', { waitUntil: 'networkidle' }); await sleep(1200);
  await page.fill('input[placeholder="ornek@eposta.com"]', email);
  await page.fill('input[type="password"]', password);
  await page.locator('[role="button"]').filter({ hasText: /^Giriş yap$/ }).last().click();
  await sleep(2500);
  fs.mkdirSync(out, { recursive: true });
  // 1. ana sayfa
  await page.goto(BASE + '/', { waitUntil: 'networkidle' }); await settle(3000); await snap('01-ana-sayfa');
  // 2. liste
  await page.goto(BASE + '/discover', { waitUntil: 'networkidle' }); await settle(3000); await snap('02-liste');
  // 3. filtre
  await page.addStyleTag({ content: '[role="dialog"]{top:54px!important;height:calc(100% - 54px)!important;border-top-left-radius:16px!important;border-top-right-radius:16px!important;overflow:hidden!important;box-shadow:0 0 0 2000px rgba(0,0,0,0.34)!important}' });
  await tap('[aria-label="Filtre"]'); await sleep(1500);
  await tap('[role="switch"]'); await sleep(400); await tap('[aria-label="Sessiz"]'); await sleep(600); await snap('03-filtre');
  // 4. benim için seç
  await page.goto(BASE + '/', { waitUntil: 'networkidle' }); await settle(2500);
  await tap('[aria-label^="Benim için seç"]'); await sleep(2500); await settle(800); await snap('04-benim-icin-sec');
  // 5. oylama
  await tap('[aria-label^="Arkadaşlarla karar ver"]'); await page.waitForURL(/\/poll\//, { timeout: 20000 }); await settle(2000);
  const rows = page.locator('[role="button"][aria-label*=" oy"]').filter({ visible: true });
  await rows.nth(1).click(); await sleep(1500); await snap('05-oylama');
  const venueId = await page.evaluate(() => null);
  // 6. mekan
  const rec = await (await fetch(API + '/api/recommendations?lat=40.9885&lng=29.025&intent=Kahve&radiusMeters=5000')).json();
  const v = rec.items.find((i) => i.name === 'Montag Coffee & Roasters') || rec.items.find((i) => i.openNow) || rec.items[0];
  console.log('mekan', v.name, v.placeId);
  await page.goto(BASE + '/venue/' + encodeURIComponent(v.placeId), { waitUntil: 'networkidle' }); await settle(3000); await snap('06-mekan');
  // 7. listeler
  await tap('[aria-label="Listeye ekle"]'); await sleep(1500);
  await page.locator('input[placeholder="Kahve rotam"]').fill('Kahve rotam'); await sleep(300);
  await tap('[aria-label="Oluştur ve ekle"]'); await sleep(1500);
  await page.goto(BASE + '/collections', { waitUntil: 'networkidle' }); await settle(2000); await snap('07-listelerim');
  // 8. gece modu
  await page.evaluate(() => localStorage.setItem('theme.mode', 'dark'));
  await page.goto(BASE + '/discover', { waitUntil: 'networkidle' }); await settle(3000); await snap('08-gece-modu');
  await browser.close();
})();
