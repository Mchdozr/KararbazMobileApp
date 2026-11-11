import { DEFAULT_API } from '@/lib/constants';
import type { GeoHit } from '@/lib/geo-hit';
import type { Preferences, PromotionItem, ReviewItem, RouteMode, RouteResult, SavedPlace, Session, VenueCard, VenueDetail } from '@/lib/types';

let apiBase = DEFAULT_API;
let accessToken: string | null = null;
let refreshToken: string | null = null;
let refreshWait: Promise<boolean> | null = null;
let authEvents: { onRefreshed?: (session: Session) => void; onCleared?: () => void } = {};

const EMPTY_PREFS: Preferences = { defaultRadiusMeters: 1500, intents: [], vibes: [] };
const photoWait = new Map<string, Promise<string | null>>();

export function setApiBase(url: string) {
  apiBase = url.replace(/\/$/, '');
}

export async function pickReachableApiBase(saved?: string | null) {
  const candidates = [DEFAULT_API, saved, 'http://192.168.110.84:5079', 'http://127.0.0.1:5079']
    .map((u) => u?.replace(/\/$/, '') ?? '')
    .filter((u, i, all) => u.startsWith('http') && all.indexOf(u) === i);
  for (const base of candidates) {
    try {
      const ctrl = new AbortController();
      const timer = setTimeout(() => ctrl.abort(), 4000);
      const res = await fetch(`${base}/health`, { signal: ctrl.signal });
      clearTimeout(timer);
      if (res.ok) {
        apiBase = base;
        return base;
      }
    } catch {
      /* try next */
    }
  }
  apiBase = DEFAULT_API.replace(/\/$/, '');
  return apiBase;
}

export function getApiBase() {
  return apiBase;
}

export function setAuthEvents(events: typeof authEvents) {
  authEvents = events;
}

export function applySession(session: Session | null) {
  accessToken = session?.accessToken ?? null;
  refreshToken = session?.refreshToken ?? null;
}

export function setAccessToken(token: string | null) {
  accessToken = token;
  if (!token) {
    refreshToken = null;
  }
}

function jwtExpired(token: string, skewMs = 15_000): boolean {
  try {
    const part = token.split('.')[1];
    if (!part) {
      return true;
    }
    const padded = part.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - (part.length % 4)) % 4);
    const payload = JSON.parse(globalThis.atob(padded)) as { exp?: number };
    return typeof payload.exp !== 'number' || payload.exp * 1000 < Date.now() + skewMs;
  } catch {
    return true;
  }
}

async function refreshAccess(): Promise<boolean> {
  if (!refreshToken) {
    return false;
  }
  if (refreshWait) {
    return refreshWait;
  }
  refreshWait = (async () => {
    try {
      const res = await fetch(`${apiBase}/api/auth/refresh`, {
        method: 'POST',
        headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      const text = await res.text();
      if (!res.ok) {
        if (res.status === 401) {
          applySession(null);
          authEvents.onCleared?.();
        }
        return false;
      }
      const session = JSON.parse(text) as Session;
      applySession(session);
      authEvents.onRefreshed?.(session);
      return Boolean(session.accessToken);
    } catch {
      return false;
    }
  })().finally(() => {
    refreshWait = null;
  });
  return refreshWait;
}

export async function hydrateAuth() {
  await ensureFreshAccess();
}

async function ensureFreshAccess() {
  if (accessToken && !jwtExpired(accessToken)) {
    return;
  }
  if (refreshToken) {
    await refreshAccess();
    return;
  }
  if (accessToken && jwtExpired(accessToken)) {
    accessToken = null;
  }
}

async function raw(path: string, init: RequestInit = {}, skipAuth = false) {
  if (!skipAuth) {
    await ensureFreshAccess();
  }
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...(init.body ? { 'Content-Type': 'application/json' } : {}),
    ...(init.headers as Record<string, string> | undefined),
  };
  if (!skipAuth && accessToken && !jwtExpired(accessToken, 0)) {
    headers.Authorization = `Bearer ${accessToken}`;
  }
  const res = await fetch(`${apiBase}${path}`, { ...init, headers });
  return { ok: res.ok, status: res.status, text: await res.text() };
}

async function request<T>(
  path: string,
  init: RequestInit = {},
  opts: { skipAuth?: boolean; optional?: boolean } = {},
): Promise<T> {
  let result = await raw(path, init, opts.skipAuth);
  if (result.status === 401 && !opts.skipAuth && refreshToken) {
    const refreshed = await refreshAccess();
    if (refreshed) {
      result = await raw(path, init, false);
    }
  }
  if (!result.ok) {
    if (opts.optional) {
      return undefined as T;
    }
    throw new Error(result.text || `${result.status}`);
  }
  return result.text ? (JSON.parse(result.text) as T) : (undefined as T);
}

export const api = {
  register: (email: string, password: string, displayName: string, acceptPrivacy = false) =>
    request<Session>(
      '/api/auth/register',
      { method: 'POST', body: JSON.stringify({ email, password, displayName, acceptPrivacy }) },
      { skipAuth: true },
    ),
  login: (email: string, password: string) =>
    request<Session>(
      '/api/auth/login',
      { method: 'POST', body: JSON.stringify({ email, password }) },
      { skipAuth: true },
    ),
  logout: (token: string) =>
    request('/api/auth/logout', { method: 'POST', body: JSON.stringify({ refreshToken: token }) }, { optional: true }),
  recommend: async (
    lat: number,
    lng: number,
    intent: string,
    openNow: boolean,
    vibes: string[],
    radiusMeters = 1500,
    skip = 0,
    take = 20,
  ) => {
    const res = await request<{ items?: VenueCard[]; hasMore?: boolean }>(
      `/api/recommendations?lat=${lat}&lng=${lng}&intent=${intent}&radiusMeters=${radiusMeters}&openNow=${openNow}&skip=${skip}&take=${take}${vibes.length ? `&vibes=${vibes.join(',')}` : ''}`,
    );
    return { items: res.items ?? [], hasMore: Boolean(res.hasMore) };
  },
  placePhoto: async (placeId: string) => {
    const hit = photoWait.get(placeId);
    if (hit) {
      return hit;
    }
    const pending = request<{ photoUrl?: string } | undefined>(
      `/api/places/${encodeURIComponent(placeId)}/photo`,
      {},
      { skipAuth: true, optional: true },
    ).then((data) => data?.photoUrl ?? null);
    photoWait.set(placeId, pending);
    return pending;
  },
  route: (
    fromLat: number,
    fromLng: number,
    toLat: number,
    toLng: number,
    mode: RouteMode,
  ) =>
    request<RouteResult | undefined>(
      `/api/routes?fromLat=${fromLat}&fromLng=${fromLng}&toLat=${toLat}&toLng=${toLng}&mode=${mode}`,
      {},
      { skipAuth: true, optional: true },
    ),
  venue: async (placeId: string) =>
    mapVenueDetail(await request<Record<string, unknown>>(`/api/venues/${encodeURIComponent(placeId)}`)),
  reviews: (placeId: string) => request<ReviewItem[]>(`/api/venues/${encodeURIComponent(placeId)}/reviews`),
  writeReview: (placeId: string, stars: number, text: string, tags: string[], photoUrls: string[] = []) =>
    request(`/api/venues/${encodeURIComponent(placeId)}/reviews`, {
      method: 'POST',
      body: JSON.stringify({ stars, text, tags, photoUrls }),
    }),
  favorite: (placeId: string) =>
    request(`/api/venues/${encodeURIComponent(placeId)}/favorite`, { method: 'POST' }, { optional: true }),
  unfavorite: (placeId: string) =>
    request(`/api/venues/${encodeURIComponent(placeId)}/favorite`, { method: 'DELETE' }, { optional: true }),
  isFavorite: async (placeId: string) => {
    const data = await request<{ saved?: boolean } | undefined>(
      `/api/venues/${encodeURIComponent(placeId)}/favorite`,
      {},
      { optional: true },
    );
    return Boolean(data?.saved);
  },
  report: (placeId: string, reason: string, reviewId?: string) =>
    request(
      `/api/venues/${encodeURIComponent(placeId)}/reports`,
      { method: 'POST', body: JSON.stringify({ reason, reviewId: reviewId ?? null }) },
      { optional: true },
    ),
  favorites: async () => {
    const data = await request<VenueCard[] | undefined>('/api/me/favorites', {}, { optional: true });
    return data ?? [];
  },
  search: (q: string, lat: number, lng: number) =>
    request<VenueCard[]>(`/api/search?q=${encodeURIComponent(q)}&lat=${lat}&lng=${lng}`),
  preferences: async () => {
    await ensureFreshAccess();
    if (!accessToken) {
      return EMPTY_PREFS;
    }
    const data = await request<Preferences | undefined>('/api/me/preferences', {}, { optional: true });
    return data ?? EMPTY_PREFS;
  },
  savePreferences: async (p: Preferences) => {
    const saved = await request<Preferences | undefined>(
      '/api/me/preferences',
      { method: 'PUT', body: JSON.stringify(p) },
      { optional: true },
    );
    return saved ?? null;
  },
  consent: (purpose: string, granted: boolean) =>
    request('/api/me/consent', { method: 'POST', body: JSON.stringify({ purpose, granted }) }, { optional: true }),
  claim: (placeId: string, message: string) =>
    request(`/api/venues/${encodeURIComponent(placeId)}/claims`, {
      method: 'POST',
      body: JSON.stringify({ message }),
    }),
  updateOwner: (placeId: string, displayName: string, description: string) =>
    request(`/api/venues/${encodeURIComponent(placeId)}/owner`, {
      method: 'PUT',
      body: JSON.stringify({ displayName, description }),
    }),
  uploadOwnerPhoto: (placeId: string, base64: string) =>
    request<{ url: string }>(`/api/venues/${encodeURIComponent(placeId)}/photos`, {
      method: 'POST',
      body: JSON.stringify({ base64, contentType: 'image/jpeg' }),
    }),
  replyReview: (reviewId: string, text: string) =>
    request(`/api/venues/reviews/${reviewId}/reply`, { method: 'POST', body: JSON.stringify({ text }) }),
  createPromotion: (body: object) => request<{ id: string; status: string }>('/api/promotions', { method: 'POST', body: JSON.stringify(body) }),
  checkout: (id: string) => request<{ status: string }>(`/api/promotions/${id}/checkout`, { method: 'POST' }),
  myPromotions: async () => {
    const data = await request<PromotionItem[] | undefined>('/api/promotions/mine', {}, { optional: true });
    return data ?? [];
  },
  deleteMe: () => request('/api/auth/me', { method: 'DELETE' }),
  geoSuggest: async (q: string): Promise<GeoHit[]> => {
    const data = await request<GeoHit[]>(
      `/api/geo/suggest?q=${encodeURIComponent(q)}`,
      {},
      { skipAuth: true, optional: true },
    );
    return data ?? [];
  },
  savedPlaces: async () => {
    const data = await request<SavedPlace[] | undefined>('/api/me/places', {}, { optional: true });
    return data ?? [];
  },
  addSavedPlace: async (body: { label: string; kind: string; latitude: number; longitude: number }) =>
    request<SavedPlace | undefined>('/api/me/places', { method: 'POST', body: JSON.stringify(body) }, { optional: true }),
  removeSavedPlace: (id: string) => request(`/api/me/places/${id}`, { method: 'DELETE' }, { optional: true }),
  uploadAvatar: async (base64: string) =>
    request<{ avatarUrl: string } | undefined>(
      '/api/me/avatar',
      { method: 'POST', body: JSON.stringify({ base64, contentType: 'image/jpeg' }) },
      { optional: true },
    ),
  uploadPhoto: async (base64: string) =>
    request<{ url: string } | undefined>(
      '/api/me/photos',
      { method: 'POST', body: JSON.stringify({ base64, contentType: 'image/jpeg' }) },
      { optional: true },
    ),
};

export function mediaUrl(path?: string | null) {
  if (!path) {
    return null;
  }
  if (path.startsWith('http://') || path.startsWith('https://')) {
    return path;
  }
  return `${apiBase}${path}`;
}

export function mapVenueDetail(raw: Record<string, unknown>): VenueDetail {
  const google = (raw.google as Record<string, unknown> | undefined) ?? {};
  const nereye = (raw.nereye as Record<string, unknown> | undefined) ?? {};
  const ownerPhotos = (nereye.photoUrls as string[] | undefined) ?? [];
  const osmPhoto =
    typeof google.photoReference === 'string'
    && (google.photoReference.startsWith('http')
      || google.photoReference.startsWith('/media/')
      || google.photoReference.startsWith('/api/media/')
      || google.photoReference.startsWith('places/'))
      ? google.photoReference
      : undefined;
  const photoUrls = [...ownerPhotos, osmPhoto].filter((u): u is string => Boolean(u));
  return {
    placeId: raw.placeId as string,
    name: (nereye.ownerDisplayName as string) || (google.name as string),
    address: google.address as string | undefined,
    openNow: google.openNow as boolean | undefined,
    googleRating: google.googleRating as number | undefined,
    googleRatingCount: google.googleRatingCount as number | undefined,
    nereyeRating: nereye.ratingAvg as number | undefined,
    nereyeRatingCount: (nereye.ratingCount as number) ?? 0,
    claimed: (nereye.claimed as boolean) ?? false,
    mapsUri: google.googleMapsUri as string | undefined,
    ownerDescription: nereye.ownerDescription as string | undefined,
    attribution: (google.attribution as string) ?? 'Harita verisi: OpenStreetMap',
    vibes: (nereye.vibes as string[]) ?? [],
    events: (raw.events as VenueDetail['events']) ?? [],
    latitude: (google.latitude as number) ?? 0,
    longitude: (google.longitude as number) ?? 0,
    photoUrl: photoUrls[0],
    photoUrls,
    note: (raw.note as string | undefined) || (nereye.ownerDescription as string | undefined),
    phone: google.phone as string | undefined,
    website: google.website as string | undefined,
    openingHours: google.openingHours as string | undefined,
    isFavorite: (nereye.isFavorite as boolean) ?? false,
    geohash: nereye.geohash as string | undefined,
    cuisine: google.cuisine as string | undefined,
    outdoorSeating: google.outdoorSeating as string | undefined,
    internetAccess: google.internetAccess as string | undefined,
    amenityChips: (raw.amenityChips as string[]) ?? [],
  };
}
