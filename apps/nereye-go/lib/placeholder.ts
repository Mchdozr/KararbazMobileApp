const PALETTES = [
  ['#fbf6ee', '#064e3b'],
  ['#fffdf8', '#0a6b50'],
  ['#d7e8dc', '#064e3b'],
  ['#f4e6c8', '#04382b'],
  ['#cfe0c8', '#064e3b'],
  ['#f8ead4', '#0a6b50'],
  ['#c9ddd4', '#04382b'],
  ['#ebe3d2', '#064e3b'],
] as const;

export function hashSeed(value: string) {
  let h = 2166136261;
  for (let i = 0; i < value.length; i += 1) {
    h ^= value.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

export function placeholderPalette(placeId: string) {
  return PALETTES[hashSeed(placeId) % PALETTES.length];
}

export function placeholderInitials(name?: string | null, placeId?: string) {
  const words = (name ?? '').trim().split(/\s+/).filter(Boolean);
  if (words.length >= 2) {
    return (words[0][0] + words[1][0]).toLocaleUpperCase('tr-TR');
  }
  if (words[0]?.length) {
    return words[0].slice(0, 2).toLocaleUpperCase('tr-TR');
  }
  return (placeId ?? 'NY').replace(/[^a-z0-9]/gi, '').slice(-2).toUpperCase() || 'NY';
}

export function placeholderShapes(placeId: string) {
  const h = hashSeed(placeId);
  return [
    { top: 8 + (h % 18), left: -12 + ((h >> 3) % 24), size: 72 + (h % 36), opacity: 0.28 },
    { top: 40 + ((h >> 5) % 30), left: 48 + ((h >> 7) % 40), size: 88 + ((h >> 2) % 28), opacity: 0.2 },
    { top: 90 + ((h >> 8) % 20), left: 10 + ((h >> 4) % 30), size: 56 + ((h >> 6) % 24), opacity: 0.22 },
  ];
}
