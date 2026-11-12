import { getItem, removeItem, setItem } from '@/lib/storage';

const KEY = 'nereye.recentSearches';

export async function loadRecentSearches(): Promise<string[]> {
  try {
    const raw = await getItem(KEY);
    if (!raw) {
      return [];
    }
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string').slice(0, 8) : [];
  } catch {
    return [];
  }
}

export async function pushRecentSearch(q: string): Promise<string[]> {
  const trimmed = q.trim();
  if (trimmed.length < 2) {
    return loadRecentSearches();
  }
  const prev = await loadRecentSearches();
  const next = [trimmed, ...prev.filter((x) => x.toLowerCase() !== trimmed.toLowerCase())].slice(0, 8);
  try {
    await setItem(KEY, JSON.stringify(next));
  } catch {
    /* web */
  }
  return next;
}

export async function removeRecentSearch(q: string): Promise<string[]> {
  const next = (await loadRecentSearches()).filter((x) => x !== q);
  await setItem(KEY, JSON.stringify(next));
  return next;
}

export async function clearRecentSearches(): Promise<string[]> {
  await removeItem(KEY);
  return [];
}
