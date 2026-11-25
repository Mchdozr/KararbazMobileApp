import { strict as assert } from 'node:assert';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';

function files(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const full = join(dir, name);
    return statSync(full).isDirectory() ? files(full) : full.endsWith('.tsx') ? [full] : [];
  });
}

test('seçili/işaretli durumu web ekran okuyucusuna da gider (aria-checked / aria-selected)', () => {
  const root = new URL('..', import.meta.url).pathname;
  const missing: string[] = [];
  for (const file of [...files(join(root, 'app')), ...files(join(root, 'components'))]) {
    const src = readFileSync(file, 'utf8');
    for (const m of src.matchAll(/accessibilityState=\{\{ (checked|selected): ([^,}]+)/g)) {
      const after = src.slice(m.index!, m.index! + 400);
      if (!after.includes(`aria-${m[1]}=`)) {
        missing.push(`${file.replace(root, '')}: ${m[0]}`);
      }
    }
  }
  assert.deepEqual(missing, []);
});
