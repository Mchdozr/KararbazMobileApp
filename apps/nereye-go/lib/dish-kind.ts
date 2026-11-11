const NAME_KIND: [string, string][] = [
  ['müze', 'museum'],
  ['muze', 'museum'],
  ['museum', 'museum'],
  ['miniatürk', 'museum'],
  ['miniaturk', 'museum'],
  ['tiyatro', 'museum'],
  ['galeri', 'museum'],
  ['kütüphane', 'museum'],
  ['kutuphane', 'museum'],
  ['kültür', 'museum'],
  ['kultur', 'museum'],
  ['park', 'park'],
  ['sahil', 'park'],
  ['plaj', 'park'],
  ['bahçe', 'park'],
  ['bahce', 'park'],
  ['pub', 'pub'],
  ['birahane', 'pub'],
  ['bira', 'pub'],
  ['lounge', 'lounge'],
  ['kokteyl', 'lounge'],
  ['cocktail', 'lounge'],
  ['kafe', 'coffee'],
  ['cafe', 'coffee'],
  ['kahve', 'coffee'],
  ['coffee', 'coffee'],
  ['espresso', 'coffee'],
];

const FOOD_KIND: [string, string][] = [
  ['döner', 'doner'],
  ['doner', 'doner'],
  ['dürüm', 'doner'],
  ['pizza', 'pizza'],
  ['burger', 'burger'],
  ['hamburger', 'burger'],
  ['udon', 'noodles'],
  ['udonya', 'noodles'],
  ['ramen', 'noodles'],
  ['noodle', 'noodles'],
  ['erişte', 'noodles'],
  ['sushi', 'sushi'],
  ['japon', 'sushi'],
  ['japanese', 'sushi'],
  ['çin', 'chinese'],
  ['chinese', 'chinese'],
  ['wok', 'chinese'],
  ['balık', 'seafood'],
  ['balik', 'seafood'],
  ['seafood', 'seafood'],
  ['midye', 'seafood'],
  ['balıkçı', 'seafood'],
  ['tavuk', 'chicken'],
  ['chicken', 'chicken'],
  ['biftek', 'steak'],
  ['steak', 'steak'],
  ['bonfile', 'steak'],
  ['kahvaltı', 'breakfast'],
  ['kahvalti', 'breakfast'],
  ['breakfast', 'breakfast'],
  ['börek', 'breakfast'],
  ['borek', 'breakfast'],
  ['simit', 'breakfast'],
  ['tatlı', 'dessert'],
  ['tatli', 'dessert'],
  ['baklava', 'dessert'],
  ['dondurma', 'dessert'],
  ['waffle', 'dessert'],
  ['dessert', 'dessert'],
  ['pastane', 'dessert'],
  ['makarna', 'pasta'],
  ['spaghetti', 'pasta'],
  ['carbonara', 'pasta'],
  ['italyan', 'pasta'],
  ['italian', 'pasta'],
  ['lahmacun', 'kebab'],
  ['pide', 'kebab'],
  ['ocakbaşı', 'kebab'],
  ['ocakbasi', 'kebab'],
  ['kebap', 'kebab'],
  ['kebab', 'kebab'],
  ['şiş', 'kebab'],
  ['shawarma', 'kebab'],
  ['falafel', 'kebab'],
  ['iskender', 'kebab'],
  ['ızgara', 'kebab'],
  ['izgara', 'kebab'],
  ['kavurma', 'kebab'],
];

const GENERIC_KIND: [string, string][] = [
  ['turkish', 'kebab'],
  ['türk', 'kebab'],
];

const SUFFIXES = ['i', 'ı', 'u', 'ü', 'si', 'sı', 'ci', 'cı', 'cu', 'çu'];

function tokens(value: string) {
  return value
    .toLocaleLowerCase('tr-TR')
    .replace(/_/g, ' ')
    .split(/[^a-z0-9çğıöşü]+/)
    .filter(Boolean);
}

function sameWord(token: string, stem: string) {
  if (token === stem) {
    return true;
  }
  return SUFFIXES.some((suffix) => token === `${stem}${suffix}`);
}

function match(list: string[], table: [string, string][]) {
  for (const token of list) {
    for (const [stem, kind] of table) {
      if (sameWord(token, stem)) {
        return kind;
      }
    }
  }
  return null;
}

export function dishKind(name?: string | null, cuisine?: string | null) {
  const nameTokens = tokens(name ?? '');
  const cuisineTokens = tokens(cuisine ?? '');
  return (
    match(nameTokens, NAME_KIND)
    ?? match(nameTokens, FOOD_KIND)
    ?? match(cuisineTokens, FOOD_KIND)
    ?? match(cuisineTokens, GENERIC_KIND)
  );
}

export function fallbackPool(kind: string | null, intent?: string) {
  if (kind) {
    return kind;
  }
  if (intent === 'Kahve') {
    return 'coffee';
  }
  if (intent === 'Gece' || intent === 'Eglence') {
    return 'night';
  }
  if (intent === 'AcikHava') {
    return 'park';
  }
  if (intent === 'Kultur') {
    return 'museum';
  }
  return 'food';
}

function hashSeed(value: string) {
  let h = 2166136261;
  for (let i = 0; i < value.length; i += 1) {
    h ^= value.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

export function pickIndex(placeId: string, length: number) {
  return hashSeed(placeId || 'ny') % length;
}
