import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { routeFromUrl } from './push-route.ts';

test('bildirimdeki bağlantı uygulama içi ekrana çevrilir; yabancı adres yok sayılır', () => {
  assert.deepEqual(routeFromUrl('nereyego://venue/nereye%3Apetra'), { pathname: '/venue/[id]', params: { id: 'nereye:petra' } });
  assert.deepEqual(routeFromUrl('nereyego://venue/osm:node:123'), { pathname: '/venue/[id]', params: { id: 'osm:node:123' } });
  assert.deepEqual(routeFromUrl('nereyego://poll/ab3k9mnp'), { pathname: '/poll/[code]', params: { code: 'ab3k9mnp' } });
  assert.deepEqual(routeFromUrl('nereyego://list/ab3k9mnp'), { pathname: '/list/[code]', params: { code: 'ab3k9mnp' } });
  assert.equal(routeFromUrl('https://kotu.example/venue/x'), null);
  assert.equal(routeFromUrl('nereyego://admin/x'), null);
  assert.equal(routeFromUrl(undefined), null);
  assert.equal(routeFromUrl('nereyego://poll/../../x'), null);
});
