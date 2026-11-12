import { getApiBase } from '@/lib/api';

const local: Record<string, number> = {
  'nereye:petra': require('../assets/venues/petra.jpg'),
  'nereye:fazil': require('../assets/venues/fazil.jpg'),
  'nereye:walter': require('../assets/venues/walter.jpg'),
  'nereye:montag': require('../assets/venues/montag.jpg'),
  'nereye:federica': require('../assets/venues/federica.jpg'),
  'nereye:goygoy': require('../assets/venues/goygoy.jpg'),
  'nereye:opera': require('../assets/venues/opera.jpg'),
  'nereye:mandabatmaz': require('../assets/venues/mandabatmaz.jpg'),
  'nereye:kronotrop': require('../assets/venues/kronotrop.jpg'),
  'nereye:ciya': require('../assets/venues/ciya.jpg'),
  'nereye:karakoy': require('../assets/venues/karakoy.jpg'),
  'nereye:sureyya': require('../assets/venues/sureyya.jpg'),
  'nereye:zorlu': require('../assets/venues/zorlu.jpg'),
  'nereye:modern': require('../assets/venues/modern.jpg'),
  'nereye:mini': require('../assets/venues/mini.jpg'),
  'nereye:moda': require('../assets/venues/moda.jpg'),
};

function word(token: string, stem: string) {
  return token === stem || token === `${stem}i` || token === `${stem}ı` || token === `${stem}si` || token === `${stem}sı`;
}

const pools: Record<string, number[]> = {
  Kahve: [local['nereye:petra']],
  Yemek: [local['nereye:ciya'], local['nereye:karakoy'], local['nereye:fazil']],
  Eglence: [local['nereye:mini'], local['nereye:goygoy'], local['nereye:opera']],
  Kultur: [local['nereye:sureyya'], local['nereye:modern'], local['nereye:zorlu']],
  AcikHava: [local['nereye:moda'], local['nereye:federica']],
  Gece: [local['nereye:opera'], local['nereye:mini'], local['nereye:goygoy']],
};

function pick(pool: number[], placeId?: string) {
  let h = 2166136261;
  const value = placeId || 'ny';
  for (let i = 0; i < value.length; i += 1) {
    h ^= value.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return pool[(h >>> 0) % pool.length];
}

export function servicePhoto(name?: string | null, intent?: string, placeId?: string) {
  const tokens = (name ?? '').toLocaleLowerCase('tr-TR').split(/[^a-z0-9çğıöşü]+/).filter(Boolean);
  const has = (stem: string) => tokens.some((token) => word(token, stem));
  let kind = intent && pools[intent] ? intent : 'Kahve';
  if (has('müze') || has('muze') || has('tiyatro') || tokens.includes('miniatürk')) {
    kind = 'Kultur';
  } else if (has('park') || has('sahil') || has('plaj')) {
    kind = 'AcikHava';
  } else if (has('pub') || has('lounge')) {
    kind = 'Gece';
  } else if (has('restoran') || has('lokanta') || has('kebap') || has('döner') || has('doner') || has('pideci') || has('pastane')) {
    kind = 'Yemek';
  } else if (has('kahve') || has('cafe') || has('kafe') || has('çay') || has('cay')) {
    kind = 'Kahve';
  }
  return pick(pools[kind], placeId);
}

export const INTENT_PHOTOS: Record<string, number> = {
  Kahve: local['nereye:petra'],
  Yemek: local['nereye:ciya'],
  Eglence: local['nereye:mini'],
  Kultur: local['nereye:sureyya'],
  AcikHava: local['nereye:moda'],
  Gece: local['nereye:opera'],
};

export function photoSource(placeId: string, photoUrl?: string | null, photoReference?: string | null) {
  if (local[placeId]) {
    return local[placeId];
  }
  const raw = photoUrl || photoReference;
  if (raw?.startsWith('http')) {
    return { uri: raw };
  }
  if (raw?.startsWith('places/')) {
    return { uri: `${getApiBase()}/api/media/google?name=${encodeURIComponent(raw)}` };
  }
  if (raw?.startsWith('/')) {
    return { uri: `${getApiBase()}${raw}` };
  }
  return null;
}
