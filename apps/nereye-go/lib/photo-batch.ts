export const PHOTO_BATCH_MAX = 8;

export function photoBatchIds(placeIds: string[]) {
  const seen = new Set<string>();
  const ids: string[] = [];
  for (const raw of placeIds) {
    const id = raw.trim();
    if (!id || seen.has(id)) {
      continue;
    }
    seen.add(id);
    ids.push(id);
    if (ids.length === PHOTO_BATCH_MAX) {
      break;
    }
  }
  return ids;
}
