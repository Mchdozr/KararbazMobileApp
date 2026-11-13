// Mekan fotoğraflarının tekilleştirilmesi. React Native içe aktarmaz: node ile test edilir.

const PROXY = '/api/media/google';

/**
 * Aynı Google karesinin üç yazımını tek biçime indirir:
 * ham `places/…`, `/api/media/google?name=…` ve aynı yolun `http://host:port` önekli hali.
 * Google karesi olmayan adresler olduğu gibi döner.
 */
export function canonicalPhoto(url: string): string {
  const value = url.trim();
  let name: string | null = null;
  if (value.startsWith('places/')) {
    name = value;
  } else {
    const at = value.indexOf(PROXY);
    const query = at >= 0 ? value.slice(at + PROXY.length) : '';
    const match = /[?&]name=([^&#]*)/.exec(query);
    if (match) {
      try {
        name = decodeURIComponent(match[1]);
      } catch {
        name = match[1];
      }
    }
  }
  return name?.startsWith('places/') ? `${PROXY}?name=${encodeURIComponent(name)}` : value;
}

/**
 * Detay sayfasının fotoğraf listesi. İşletmenin kendi fotoğrafları önce, sonra sunucu galerisi.
 * Sunucu galerisi `photoReference`'ı zaten içerir; ham referans yalnızca galeri boşsa (eski sunucu) eklenir.
 */
export function venuePhotoUrls(input: {
  ownerPhotos?: readonly (string | null | undefined)[];
  gallery?: readonly (string | null | undefined)[];
  photoReference?: string | null;
}): string[] {
  const gallery = (input.gallery ?? []).filter((u): u is string => Boolean(u?.trim()));
  const tail = gallery.length === 0 && isPhotoRef(input.photoReference) ? [input.photoReference] : [];
  const seen = new Set<string>();
  const out: string[] = [];
  for (const raw of [...(input.ownerPhotos ?? []), ...gallery, ...tail]) {
    if (!raw?.trim()) {
      continue;
    }
    const canon = canonicalPhoto(raw);
    if (!seen.has(canon)) {
      seen.add(canon);
      out.push(canon);
    }
  }
  return out;
}

function isPhotoRef(value?: string | null): value is string {
  return (
    typeof value === 'string'
    && (value.startsWith('http') || value.startsWith('/media/') || value.startsWith('/api/media/') || value.startsWith('places/'))
  );
}

export type ResolvedPhoto = { raw: string; uri: string };

/**
 * Ekrana basılacak kareler: çözülmüş URI'ye göre tekil (React anahtarı da bu URI'dir) ve
 * indirilemediği bilinenler çıkarılmış. Sıra korunur.
 */
export function displayPhotos(
  raws: readonly (string | null | undefined)[],
  resolve: (raw: string) => string | null,
  failed?: ReadonlySet<string>,
): ResolvedPhoto[] {
  const seen = new Set<string>();
  const out: ResolvedPhoto[] = [];
  for (const raw of raws) {
    if (!raw) {
      continue;
    }
    const uri = resolve(raw);
    if (!uri || seen.has(uri) || failed?.has(uri)) {
      continue;
    }
    seen.add(uri);
    out.push({ raw, uri });
  }
  return out;
}

export function uniqueStrings(values: readonly (string | null | undefined)[]): string[] {
  return [...new Set(values.filter((v): v is string => Boolean(v)))];
}
