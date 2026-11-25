import { strict as assert } from 'node:assert';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { decimal, resolveLanguage, setLanguage, t } from './i18n.ts';
import { en } from './locales/en.ts';

function files(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      return name === 'node_modules' || name === 'locales' ? [] : files(full);
    }
    return /\.(tsx?|ts)$/.test(name) && !name.endsWith('.test.ts') ? [full] : [];
  });
}

/** Kaynakta t('…') ile sarılmış düz metinler. */
function usedKeys(): Map<string, string> {
  const root = new URL('..', import.meta.url).pathname;
  const keys = new Map<string, string>();
  for (const file of [...files(join(root, 'app')), ...files(join(root, 'components')), ...files(join(root, 'lib'))]) {
    const src = readFileSync(file, 'utf8');
    for (const m of src.matchAll(/\bt\(\s*(['"])((?:\\.|(?!\1).)*)\1/g)) {
      keys.set(m[2].replace(/\\'/g, "'").replace(/\\"/g, '"').replace(/\\n/g, '\n'), file.replace(root, ''));
    }
  }
  return keys;
}

test('kaynakta t() ile sarılan her metnin İngilizcesi var ve yer tutucuları aynı', () => {
  const missing: string[] = [];
  const badParams: string[] = [];
  for (const [key, file] of usedKeys()) {
    if (!(key in en)) {
      missing.push(`${file}: ${key}`);
      continue;
    }
    const want = [...key.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort().join(',');
    const got = [...en[key].matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort().join(',');
    if (want !== got) {
      badParams.push(`${key} -> ${en[key]}`);
    }
  }
  assert.deepEqual(missing, []);
  assert.deepEqual(badParams, []);
});

test('çeviri ve yer tutucu', () => {
  setLanguage('en');
  assert.equal(t('Keşfet'), 'Discover');
  assert.equal(t('{n} dk yürüme', { n: 8 }), '8 min walk');
  assert.equal(t('Sözlükte olmayan metin'), 'Sözlükte olmayan metin');
  assert.equal(decimal(4.6), '4.6');
  assert.equal(t('Ara|telefon'), 'Call');
  assert.equal(t('Ara'), 'Search');
  setLanguage('tr');
  assert.equal(t('Keşfet'), 'Keşfet');
  assert.equal(t('{n} dk yürüme', { n: 8 }), '8 dk yürüme');
  assert.equal(decimal(4.6), '4,6');
  assert.equal(t('Ara|telefon'), 'Ara');
});

test('dil seçimi: tercih cihaz dilini ezer; Türkçe olmayan cihaz İngilizce', () => {
  assert.equal(resolveLanguage('en', 'tr'), 'en');
  assert.equal(resolveLanguage('tr', 'de'), 'tr');
  assert.equal(resolveLanguage('system', 'tr'), 'tr');
  assert.equal(resolveLanguage(null, 'de'), 'en');
  assert.equal(resolveLanguage(null, null), 'tr');
});
