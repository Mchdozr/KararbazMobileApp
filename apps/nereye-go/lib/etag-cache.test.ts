import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { EtagCache } from './etag-cache.ts';

test('etiketli gövde saklanır, If-None-Match başlığı üretilir', () => {
  const cache = new EtagCache(3);
  assert.deepEqual(cache.headers('/a'), {});
  cache.remember('/a', 200, 'W/"x1"', '{"items":[]}');
  assert.deepEqual(cache.headers('/a'), { 'If-None-Match': 'W/"x1"' });
});

test('304 gelince saklanan gövde 200 gibi döner', () => {
  const cache = new EtagCache(3);
  cache.remember('/a', 200, 'W/"x1"', '{"n":1}');
  assert.deepEqual(cache.resolve('/a', { ok: false, status: 304, text: '' }), { ok: true, status: 200, text: '{"n":1}' });
});

test('saklanan gövde yokken 304 olduğu gibi kalır; etiketsiz ya da hatalı yanıt saklanmaz', () => {
  const cache = new EtagCache(3);
  assert.deepEqual(cache.resolve('/b', { ok: false, status: 304, text: '' }), { ok: false, status: 304, text: '' });
  cache.remember('/b', 200, null, '{}');
  cache.remember('/c', 500, 'W/"e"', 'hata');
  assert.deepEqual(cache.headers('/b'), {});
  assert.deepEqual(cache.headers('/c'), {});
});

test('en eski kayıt sınırı aşınca düşer; kullanılan kayıt tazelenir', () => {
  const cache = new EtagCache(2);
  cache.remember('/a', 200, 'W/"a"', 'A');
  cache.remember('/b', 200, 'W/"b"', 'B');
  cache.resolve('/a', { ok: false, status: 304, text: '' });
  cache.remember('/c', 200, 'W/"c"', 'C');
  assert.deepEqual(cache.headers('/b'), {});
  assert.deepEqual(cache.headers('/a'), { 'If-None-Match': 'W/"a"' });
  assert.equal(cache.size, 2);
});

test('çok büyük gövde saklanmaz (bellek sınırı)', () => {
  const cache = new EtagCache(3, 10);
  cache.remember('/big', 200, 'W/"b"', 'x'.repeat(11));
  assert.deepEqual(cache.headers('/big'), {});
});
