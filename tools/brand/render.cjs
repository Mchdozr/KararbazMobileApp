const { chromium } = require('playwright');
const [,, dir, out, qs] = process.argv;
(async () => {
  const b = await chromium.launch({ args: ['--allow-file-access-from-files'] });
  const p = await b.newPage({ viewport: { width: 1024, height: 1024 } });
  await p.goto(`file://${dir}/mark.html?${qs}`); await p.waitForFunction(() => window.done); await p.waitForTimeout(200);
  await p.screenshot({ path: out, omitBackground: true }); await b.close();
})();
