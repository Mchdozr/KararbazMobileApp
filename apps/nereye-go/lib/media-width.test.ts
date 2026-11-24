import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { CARD_IMAGE_PT, variantWidth, withVariant } from './media-width.ts';

test('görüntü genişliği x piksel yoğunluğu bilinen sürüme yukarı yuvarlanır', () => {
  assert.equal(variantWidth(CARD_IMAGE_PT, 2), 400);
  assert.equal(variantWidth(CARD_IMAGE_PT, 3), 800);
  assert.equal(variantWidth(393, 3), 1200);
  assert.equal(variantWidth(430, 3), null); // 1290 px: özgün
  assert.equal(variantWidth(0, 3), null);
});

test('yalnızca yerel mekan ve yükleme görsellerine w eklenir', () => {
  assert.equal(withVariant('/media/venues/petra.jpg', 400), '/media/venues/petra.jpg?w=400');
  assert.equal(withVariant('http://10.0.2.2:5079/media/uploads/a.webp', 800), 'http://10.0.2.2:5079/media/uploads/a.webp?w=800');
  assert.equal(withVariant('/media/uploads/a.jpg?v=2', 800), '/media/uploads/a.jpg?v=2&w=800');
  assert.equal(withVariant('/media/avatars/u.jpg', 400), '/media/avatars/u.jpg');
  assert.equal(withVariant('/api/media/google?name=places/x', 400), '/api/media/google?name=places/x');
  assert.equal(withVariant('https://upload.wikimedia.org/a.jpg', 400), 'https://upload.wikimedia.org/a.jpg');
  assert.equal(withVariant('/media/venues/petra.jpg', null), '/media/venues/petra.jpg');
  assert.equal(withVariant('/media/venues/petra.jpg?w=400', 800), '/media/venues/petra.jpg?w=400');
});
