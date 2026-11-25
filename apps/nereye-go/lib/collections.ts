// Adlandırılmış listeler. Sunucudaki Nereye.Domain.Collections.CollectionRules aynası.
// React Native içe aktarmaz: node ile test edilir.

export const LIST_NAME_MAX = 40;

export type ListItem = {
  placeId: string;
  name: string;
  category: string | null;
  photoUrl: string | null;
  latitude: number;
  longitude: number;
  addedAt?: string;
};

export type ListShare = { code: string; appUrl: string; webUrl: string | null };

export type ListSummary = { id: string; name: string; count: number; coverUrl: string | null; shared: boolean; contains: boolean };

export type ListDetail = { id: string; name: string; items: ListItem[]; share: ListShare | null };

export type PublicList = { name: string; items: ListItem[]; updatedAt: string };

export function cleanListName(name: string): string | null {
  const v = name.replace(/\s+/g, ' ').trim();
  return v.length === 0 || v.length > LIST_NAME_MAX ? null : v;
}

export function itemFrom(v: {
  placeId: string;
  name: string;
  category?: string | null;
  photoUrl?: string | null;
  latitude: number;
  longitude: number;
}): ListItem {
  return {
    placeId: v.placeId,
    name: v.name,
    category: v.category ?? null,
    photoUrl: v.photoUrl ?? null,
    latitude: v.latitude,
    longitude: v.longitude,
  };
}

export function listShareText(name: string, placeNames: readonly string[], share: ListShare): string {
  const head = placeNames.slice(0, 3).join(' · ');
  const more = placeNames.length > 3 ? ` ve ${placeNames.length - 3} mekan daha` : '';
  const lines = [`${name} — ${placeNames.length} mekan`, `${head}${more}`];
  if (share.webUrl) {
    lines.push(share.webUrl);
  }
  lines.push(`Uygulamada: ${share.appUrl}`);
  return lines.join('\n');
}
