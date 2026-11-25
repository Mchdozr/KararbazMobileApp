// Bildirime dokununca açılacak ekran. Yalnızca uygulamanın kendi şeması ve bilinen ekranlar.
// React Native içe aktarmaz: node ile test edilir.

export type PushRoute =
  | { pathname: '/venue/[id]'; params: { id: string } }
  | { pathname: '/poll/[code]'; params: { code: string } }
  | { pathname: '/list/[code]'; params: { code: string } };

export function routeFromUrl(url: unknown): PushRoute | null {
  if (typeof url !== 'string') {
    return null;
  }
  const m = /^nereyego:\/\/(venue|poll|list)\/([^/?#]+)$/.exec(url.trim());
  if (!m) {
    return null;
  }
  let value: string;
  try {
    value = decodeURIComponent(m[2]);
  } catch {
    return null;
  }
  if (m[1] === 'venue') {
    return /^[A-Za-z0-9:_\-./]{1,128}$/.test(value) && !value.includes('..') ? { pathname: '/venue/[id]', params: { id: value } } : null;
  }
  if (!/^[23456789abcdefghjkmnpqrstuvwxyz]{8}$/.test(value)) {
    return null;
  }
  return m[1] === 'poll' ? { pathname: '/poll/[code]', params: { code: value } } : { pathname: '/list/[code]', params: { code: value } };
}
