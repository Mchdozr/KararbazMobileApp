import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { cleanListName, itemFrom, listShareText, LIST_NAME_MAX } from './collections.ts';

test('liste adı sunucudaki CollectionRules aynası', () => {
  assert.equal(cleanListName('  Hafta   sonu  '), 'Hafta sonu');
  assert.equal(cleanListName('   '), null);
  assert.equal(cleanListName('a'.repeat(LIST_NAME_MAX + 1)), null);
});

test('mekan sayfasından liste kaydı', () => {
  assert.deepEqual(
    itemFrom({ placeId: 'nereye:petra', name: 'Petra', category: 'Kahve dükkanı', photoUrl: '/media/venues/petra.jpg', latitude: 40.98, longitude: 29.02 }),
    { placeId: 'nereye:petra', name: 'Petra', category: 'Kahve dükkanı', photoUrl: '/media/venues/petra.jpg', latitude: 40.98, longitude: 29.02 },
  );
  assert.equal(itemFrom({ placeId: 'x', name: 'X', latitude: 1, longitude: 2 }).photoUrl, null);
});

test('paylaşım metni: ad, ilk mekanlar, bağlantılar', () => {
  const text = listShareText('Kahve rotam', ['Petra', "Walter's", 'Kronotrop', 'Federica'], { code: 'ab3k9mnp', appUrl: 'nereyego://list/ab3k9mnp', webUrl: 'https://ornek.app/l/ab3k9mnp' });
  assert.match(text, /^Kahve rotam — 4 mekan/);
  assert.match(text, /Petra · Walter's · Kronotrop ve 1 mekan daha/);
  assert.match(text, /https:\/\/ornek\.app\/l\/ab3k9mnp/);
  assert.match(text, /nereyego:\/\/list\/ab3k9mnp/);
});
