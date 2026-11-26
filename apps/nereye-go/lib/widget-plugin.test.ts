import { strict as assert } from 'node:assert';
import { createRequire } from 'node:module';
import { test } from 'node:test';

const require = createRequire(import.meta.url);
const plugin = require('../plugins/withNearbyWidget.js');

test('bileşen dosyaları paket adına göre üretilir; sağlayıcı veri dosyasını ve derin bağlantıyı kullanır', () => {
  const files = plugin.widgetFiles('com.mchdozr.nereye') as Record<string, string>;
  const kt = files['app/src/main/java/com/mchdozr/nereye/widget/NearbyWidgetProvider.kt'];
  assert.ok(kt.startsWith('package com.mchdozr.nereye.widget'));
  assert.match(kt, /import com\.mchdozr\.nereye\.R/);
  assert.match(kt, /File\(context\.filesDir, "widget-nearby\.json"\)/);
  assert.match(kt, /"nereyego:\/\/venue\/"/);
  assert.match(kt, /FLAG_IMMUTABLE/);
  assert.match(files['app/src/main/res/xml/nereye_nearby_widget_info.xml'], /updatePeriodMillis="1800000"/);
  for (const id of ['nereye_row1', 'nereye_name3', 'nereye_meta2', 'nereye_empty', 'nereye_updated', 'nereye_title']) {
    assert.match(files['app/src/main/res/layout/nereye_nearby_widget.xml'], new RegExp(`@\\+id/${id}"`), id);
  }
  assert.match(files['app/src/main/res/values/nereye_widget_strings.xml'], /Yakındaki açık mekanlar/);
  assert.match(files['app/src/main/res/values-en/nereye_widget_strings.xml'], /Open places nearby/);
  // Kesme işareti Android kaynak dosyasında kaçışlı olmalı.
  assert.match(files['app/src/main/res/values/nereye_widget_strings.xml'], /Nereye\\'yi/);
});

test('manifest: dışa kapalı alıcı bir kez eklenir', () => {
  const manifest = { manifest: { application: [{ $: { 'android:name': '.MainApplication' }, receiver: [] }] } };
  plugin.addReceiver(manifest);
  plugin.addReceiver(manifest);
  const receivers = manifest.manifest.application[0].receiver as { $: Record<string, string> }[];
  assert.equal(receivers.length, 1);
  assert.equal(receivers[0].$['android:name'], '.widget.NearbyWidgetProvider');
  assert.equal(receivers[0].$['android:exported'], 'false');
});

test('veri dosyası adı uygulama ile bileşende aynı', async () => {
  const { WIDGET_FILE } = await import('./widget-data.ts');
  assert.match(plugin.widgetFiles('a.b')['app/src/main/java/a/b/widget/NearbyWidgetProvider.kt'], new RegExp(`"${WIDGET_FILE}\\.json"`));
});
