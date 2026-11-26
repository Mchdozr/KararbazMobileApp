// Eklentinin ürettiği dosyaları (Kotlin, düzen, metinler, manifest alıcısı) bu modüle yazar ve uygulamanın
// widgetSnapshot() çıktısından örnek veri üretir. Böylece Robolectric testi, uygulamanın yazdığı dosyayı
// bileşenin okuduğu biçimle birlikte doğrular.
// Çalıştırma: node --experimental-strip-types generate.mts
import { mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { VenueCard } from '../../lib/types.ts';
import { widgetSnapshot } from '../../lib/widget-data.ts';

const here = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const plugin = require('../withNearbyWidget.js');
const pkg = require('../../app.json').expo.android.package as string;

function write(rel: string, content: string) {
  const file = join(here, rel);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, content);
}

for (const [rel, content] of Object.entries(plugin.widgetFiles(pkg) as Record<string, string>)) {
  write(rel.replace(/^app\//, ''), content);
}

// Kütüphane manifest'i: eklentinin uygulamaya eklediği alıcının aynısı.
const manifest = plugin.addReceiver({ manifest: { application: [{ $: { 'android:name': '.MainApplication' } }] } });
const receiver = manifest.manifest.application[0].receiver[0];
write(
  'src/main/AndroidManifest.xml',
  `<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <application>
        <receiver android:name="${receiver.$['android:name']}" android:exported="${receiver.$['android:exported']}" android:label="${receiver.$['android:label']}">
            <intent-filter>
                <action android:name="${receiver['intent-filter'][0].action[0].$['android:name']}" />
            </intent-filter>
            <meta-data android:name="${receiver['meta-data'][0].$['android:name']}" android:resource="${receiver['meta-data'][0].$['android:resource']}" />
        </receiver>
    </application>
</manifest>
`,
);

const card = (id: string, extra: Partial<VenueCard>): VenueCard => ({
  placeId: id,
  name: id,
  distanceMeters: 400,
  kind: 'Organic',
  nereyeRatingCount: 0,
  vibes: [],
  upcomingEvents: [],
  latitude: 40.99,
  longitude: 29.02,
  ...extra,
});

const snapshot = widgetSnapshot(
  [
    card('osm:node/101', { name: 'Kronotrop Moda', openNow: true, closesAt: '23:00', closesInMinutes: 200, distanceMeters: 640 }),
    card('sponsor', { name: 'Reklam', kind: 'Sponsored', openNow: true }),
    card('google:ChIJ x/y', { name: 'Fazıl Bey', openNow: true, distanceMeters: 1200 }),
  ],
  { label: 'Kadıköy', intent: 'Kahve', now: Date.parse('2026-11-24T17:00:00Z'), nowMinutes: 1200 },
);
write('src/test/resources/snapshot.json', JSON.stringify(snapshot));
console.log(`${pkg}: bileşen kaynakları ve örnek veri yazıldı`);
