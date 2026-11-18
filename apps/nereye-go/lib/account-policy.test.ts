import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { friendlyError } from './empty.ts';
import { MAX_NAME, MAX_PASSWORD, MIN_PASSWORD, passwordProblem } from './validation.ts';

const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');

test('client password and name limits mirror the server AccountRules', () => {
  const rules = read('../../../src/Nereye.Domain/Accounts/AccountRules.cs');
  const constant = (name: string) => Number(new RegExp(`${name} = (\\d+);`).exec(rules)?.[1]);
  assert.equal(MIN_PASSWORD, constant('MinPasswordLength'));
  assert.equal(MAX_PASSWORD, constant('MaxPasswordLength'));
  assert.equal(MAX_NAME, constant('MaxDisplayNameLength'));
  assert.equal(passwordProblem('a1'.repeat(65)), 'Şifre çok uzun.');
  assert.equal(passwordProblem('kahve1234'), null);
});

test('privacy text comes from the API and never carries an invented contact address', () => {
  const legal = read('../app/legal.tsx');
  assert.match(legal, /api\s*\.legal\(\)/);
  assert.doesNotMatch(legal, /@nereye|kvkk@/i);
  assert.doesNotMatch(legal, /const SECTIONS/);
});

test('login offers forgot password and the account screen offers verification', () => {
  assert.match(read('../app/auth.tsx'), /api\.forgotPassword\(/);
  assert.match(read('../app/account.tsx'), /api\.resendVerification\(\)/);
});

test('a feature the server switched off shows its Turkish reason, other 5xx stay generic', () => {
  const closed = { name: 'ApiError', status: 503, kind: 'http', message: 'Şifre sıfırlama şu an kapalı.' };
  assert.equal(friendlyError(closed), 'Şifre sıfırlama şu an kapalı.');
  const crash = { name: 'ApiError', status: 500, kind: 'http', message: 'NullReferenceException' };
  assert.equal(friendlyError(crash), 'Sunucuda bir sorun var. Biraz sonra tekrar dene.');
  const unverified = { name: 'ApiError', status: 403, kind: 'http', message: 'Yorum yazmak için önce e-posta adresini doğrula.' };
  assert.equal(friendlyError(unverified), 'Yorum yazmak için önce e-posta adresini doğrula.');
});

test('logout never refreshes with the token it is revoking', () => {
  const api = read('./api.ts');
  assert.match(api, /if \(!refreshToken \|\| signingOut\) \{/);
  const logout = api.slice(api.indexOf('  logout: async () => {'));
  // Sürmekte olan yenileme beklenir, token bellekten çıkarılır, sonra çıkış isteği gider.
  assert.ok(logout.indexOf('await refreshWait') < logout.indexOf('refreshToken = null'));
  assert.ok(logout.indexOf('refreshToken = null') < logout.indexOf("'/api/auth/logout'"));
  assert.match(read('./session.tsx'), /await api\.logout\(\);/);
});

test('resending verification never claims success when nothing was sent', () => {
  const account = read('../app/account.tsx');
  assert.match(account, /useFocusEffect\(/);
  const send = account.slice(account.indexOf('async function sendVerification'), account.indexOf('async function saveName'));
  assert.ok(send.indexOf('res.alreadyVerified') < send.indexOf("toast.success('Doğrulama bağlantısı"));
  assert.match(send, /if \(!res\.sent\)/);
  assert.match(read('../app/(tabs)/profile.tsx'), /emailVerified === false \? 'E-postanı doğrula'/);
});

test('review screen does not offer photos or submit to an unverified account, and reuses uploaded urls', () => {
  const review = read('../app/review/[id].tsx');
  assert.match(review, /photos\.length < REVIEW_MAX_PHOTOS && !blocked/);
  assert.match(review, /disabled=\{blocked\}/);
  assert.match(review, /x\.key === p\.key \? \{ \.\.\.x, url, base64: undefined \}/);
});
