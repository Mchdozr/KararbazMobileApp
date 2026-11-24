import { ApiError, serverMessage } from '@/lib/api-error';
import { isFresh, shareVenueRequest } from '@/lib/venue-cache';
import { venuePhotoUrls } from '@/lib/venue-photos';
import { DEFAULT_API } from '@/lib/constants';
import { apiCandidates, needsProbe } from '@/lib/release-config';
import { photoBatchIds } from '@/lib/photo-batch';
import { warmCards } from '@/lib/prefetch';
import { putRecommend, recommendCacheKey } from '@/lib/recommend-cache';
import { EtagCache } from '@/lib/etag-cache';
import { linkAbort, recommendPath } from '@/lib/request-control';
import { Platform } from 'react-native';
import type { CrashReport } from '@/lib/crash-queue';
import type { GeoHit } from '@/lib/geo-hit';
import type {
  Legal,
  MyReview,
  Preferences,
  Profile,
  PromotionItem,
  ReviewItem,
  RouteMode,
  RouteResult,
  SavedPlace,
  Session,
  VenueCard,
  VenueDetail,
} from '@/lib/types';

export { ApiError };

let apiBase = DEFAULT_API;
let accessToken: string | null = null;
let refreshToken: string | null = null;
let refreshWait: Promise<boolean> | null = null;
// Çıkış sürerken bu token'la /api/auth/refresh atılmaz (sunucuda dönmüş token'ın tekrar gelmesi hırsızlık sayılır).
let signingOut = false;
let authEvents: { onRefreshed?: (session: Session) => void; onCleared?: () => void } = {};

const EMPTY_PREFS: Preferences = { defaultRadiusMeters: 5000, intents: [], vibes: [] };
const photoWait = new Map<string, Promise<string | null>>();
const PHOTO_GROUP = 4;

function enqueuePlacePhoto(placeId: string): Promise<string | null> {
  return new Promise((resolve) => {
    photoQueue.push({ id: placeId, resolve });
    if (!photoFlush) {
      photoFlush = setTimeout(() => {
        void flushPlacePhotos();
      }, 32);
    }
  });
}

async function flushPlacePhotos() {
  photoFlush = null;
  // Küçük gruplar: sunucunun Google bütçesi (grup başına 4) ekrandaki her kartı kapsar, ilk kareler daha erken gelir.
  const batch = photoQueue.splice(0, PHOTO_GROUP);
  if (photoQueue.length > 0) {
    photoFlush = setTimeout(() => {
      void flushPlacePhotos();
    }, 32);
  }
  try {
    const ids = photoBatchIds(batch.map((row) => row.id));
    const data = await request<{ photos?: Record<string, string> } | undefined>(
      '/api/places/photos',
      { method: 'POST', body: JSON.stringify({ placeIds: ids }) },
      { skipAuth: true, optional: true },
    );
    const photos = data?.photos ?? {};
    for (const waiter of batch) {
      waiter.resolve(photos[waiter.id] ?? null);
    }
  } catch {
    for (const waiter of batch) {
      waiter.resolve(null);
    }
  }
}

export function setApiBase(url: string) {
  // Yayında adres değiştirilemez: derlemeye verilen https adres sabittir.
  if (!__DEV__) {
    return;
  }
  apiBase = url.replace(/\/$/, '');
}

async function probe(base: string): Promise<string> {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), 4000);
  try {
    const res = await fetch(`${base}/health`, { signal: ctrl.signal });
    if (!res.ok) {
      throw new Error(String(res.status));
    }
    return base;
  } finally {
    clearTimeout(timer);
  }
}

/**
 * Yanıt veren ilk API adresini seçer. Adaylar aynı anda yoklanır: sunucu kapalıyken
 * açılış ekranı adayların toplam zaman aşımı kadar değil, en fazla tek bir zaman aşımı kadar bekler.
 */
export async function pickReachableApiBase(saved?: string | null) {
  // Yayında yalnızca derlemeye verilen https adres; kayıtlı adres ve LAN yalnızca __DEV__.
  const candidates = apiCandidates(__DEV__, DEFAULT_API, saved);
  if (!needsProbe(candidates)) {
    apiBase = (candidates[0] ?? DEFAULT_API).replace(/\/$/, '');
    return apiBase;
  }
  try {
    apiBase = await Promise.any(candidates.map(probe));
  } catch {
    apiBase = (candidates[0] ?? DEFAULT_API).replace(/\/$/, '');
  }
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
  if (!refreshToken || signingOut) {
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

const TIMEOUT_MS = 45_000;
/** Soğuk Overpass 15–22 s; 15 s client abort iskeleti düşürüyor. */
const RECOMMEND_TIMEOUT_MS = 45_000;

type PhotoWaiter = { id: string; resolve: (url: string | null) => void };
const photoQueue: PhotoWaiter[] = [];
let photoFlush: ReturnType<typeof setTimeout> | null = null;

// Koşullu GET (ETag/304). Web'de tarayıcının HTTP önbelleği bunu kendisi yapar; elle başlık eklemek
// çapraz kökenli istekte ön uçuş (preflight) doğururdu. Bu yüzden yalnızca yerel uygulamada.
const etags = new EtagCache();
const conditionalGets = Platform.OS !== 'web';

type RawOpts = { skipAuth?: boolean; timeoutMs?: number; signal?: AbortSignal; conditional?: boolean };

async function raw(path: string, init: RequestInit = {}, o: RawOpts = {}) {
  const { skipAuth = false, timeoutMs = TIMEOUT_MS, signal } = o;
  const conditional = Boolean(o.conditional && conditionalGets && !init.method);
  if (!skipAuth) {
    await ensureFreshAccess();
  }
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...(init.body ? { 'Content-Type': 'application/json' } : {}),
    ...(conditional ? etags.headers(path) : {}),
    ...(init.headers as Record<string, string> | undefined),
  };
  if (!skipAuth && accessToken && !jwtExpired(accessToken, 0)) {
    headers.Authorization = `Bearer ${accessToken}`;
  }
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  const unlink = linkAbort(signal, ctrl);
  try {
    const res = await fetch(`${apiBase}${path}`, { ...init, headers, signal: ctrl.signal });
    const result = { ok: res.ok, status: res.status, text: await res.text() };
    if (!conditional) {
      return result;
    }
    etags.remember(path, res.status, res.headers.get('etag'), result.text);
    return etags.resolve(path, result);
  } catch (e) {
    if (signal?.aborted) {
      // Ekran bu isteği bıraktı (niyet/konum değişti): ağda sürmesin, hata olarak da gösterilmesin.
      throw new ApiError(0, 'aborted', 'aborted');
    }
    if (ctrl.signal.aborted) {
      throw new ApiError(0, 'timeout', 'timeout');
    }
    throw new ApiError(0, e instanceof Error ? e.message : 'network', 'network');
  } finally {
    clearTimeout(timer);
    unlink();
  }
}

type RequestOpts = RawOpts & { optional?: boolean };

async function request<T>(path: string, init: RequestInit = {}, opts: RequestOpts = {}): Promise<T> {
  let result: Awaited<ReturnType<typeof raw>>;
  try {
    result = await raw(path, init, opts);
    if (result.status === 401 && !opts.skipAuth && refreshToken) {
      const refreshed = await refreshAccess();
      if (refreshed) {
        result = await raw(path, init, { ...opts, skipAuth: false });
      }
    }
  } catch (e) {
    if (opts.optional) {
      return undefined as T;
    }
    throw e;
  }
  if (!result.ok) {
    if (opts.optional) {
      return undefined as T;
    }
    throw new ApiError(result.status, serverMessage(result.text));
  }
  if (!result.text) {
    return undefined as T;
  }
  try {
    return JSON.parse(result.text) as T;
  } catch {
    // JSON yerine HTML geldi (yanlış adres, vekil hata sayfası): ham ayrıştırma hatası kullanıcıya gitmesin.
    if (opts.optional) {
      return undefined as T;
    }
    throw new ApiError(502, '');
  }
}

export const api = {
  /** Hata raporları (lib/crash.ts). Oturumsuz; başarısızsa false döner, kuyruk sonra yeniden dener. */
  reportErrors: async (reports: readonly CrashReport[]) => {
    try {
      const res = await raw(
        '/api/telemetry/errors',
        { method: 'POST', body: JSON.stringify({ reports }) },
        { skipAuth: true, timeoutMs: 8000 },
      );
      return res.status === 202 || res.status === 400; // 400: bozuk paket, tekrar göndermenin anlamı yok
    } catch {
      return false;
    }
  },
  /** acceptTerms: kullanım koşulları kabulü, kayıt için zorunlu. Aydınlatma metni onay gerektirmez. */
  register: (email: string, password: string, displayName: string, acceptTerms: boolean) =>
    request<Session>(
      '/api/auth/register',
      { method: 'POST', body: JSON.stringify({ email, password, displayName, acceptTerms }) },
      { skipAuth: true },
    ),
  login: (email: string, password: string) =>
    request<Session>(
      '/api/auth/login',
      { method: 'POST', body: JSON.stringify({ email, password }) },
      { skipAuth: true },
    ),
  /** Kayıtlı olsun olmasın aynı yanıt; sağlayıcı yoksa 503 "Şifre sıfırlama şu an kapalı." */
  forgotPassword: (email: string) =>
    request<{ message?: string }>(
      '/api/auth/forgot',
      { method: 'POST', body: JSON.stringify({ email }) },
      { skipAuth: true },
    ),
  /** sent: mektup gitti. alreadyVerified: adres zaten doğrulanmış, mektup gitmedi. */
  resendVerification: async () => {
    const res = await request<{ sent?: boolean; alreadyVerified?: boolean } | undefined>('/api/auth/verify-email', {
      method: 'POST',
    });
    return { sent: Boolean(res?.sent), alreadyVerified: Boolean(res?.alreadyVerified) };
  },
  /** Gizlilik ve koşullar metni sunucudan gelir (tek kaynak: /gizlilik ve /kosullar sayfalarıyla aynı). */
  legal: () => request<Legal>('/api/legal', {}, { skipAuth: true }),
  /**
   * Bu cihazın oturumunu kapatır. Sürmekte olan yenileme bitince güncel token alınır ve hemen bellekten
   * silinir: çıkış isteği 401 alsa bile aynı refresh token'la yenileme denenmez. Sunucu, erişim token'ı
   * süresi dolmuş olsa da refresh token'ın kendisiyle çıkışı kabul eder.
   */
  logout: async () => {
    if (refreshWait) {
      await refreshWait.catch(() => false);
    }
    const token = refreshToken;
    signingOut = true;
    refreshToken = null;
    try {
      if (token) {
        await request(
          '/api/auth/logout',
          { method: 'POST', body: JSON.stringify({ refreshToken: token }) },
          { optional: true },
        );
      }
    } finally {
      signingOut = false;
    }
  },
  recommend: async (
    lat: number,
    lng: number,
    intent: string,
    openNow: boolean,
    vibes: string[],
    radiusMeters = 5000,
    skip = 0,
    take = 20,
    opts: { signal?: AbortSignal } = {},
  ) => {
    const key = recommendCacheKey({ lat, lng, intent, radiusMeters, openNow, vibes, skip, take });
    const res = await request<{ items?: VenueCard[]; hasMore?: boolean }>(
      recommendPath({ lat, lng, intent, radiusMeters, openNow, vibes, skip, take }),
      {},
      { timeoutMs: RECOMMEND_TIMEOUT_MS, signal: opts.signal, conditional: true },
    );
    const page = { items: res.items ?? [], hasMore: Boolean(res.hasMore) };
    putRecommend(key, page);
    warmCards(page.items, skip === 0 ? 8 : 4);
    return page;
  },
  placePhotos: async (placeIds: string[]) => {
    const ids = photoBatchIds(placeIds);
    if (ids.length === 0) {
      return {} as Record<string, string>;
    }
    const data = await request<{ photos?: Record<string, string> } | undefined>(
      '/api/places/photos',
      { method: 'POST', body: JSON.stringify({ placeIds: ids }) },
      { skipAuth: true, optional: true },
    );
    return data?.photos ?? {};
  },
  placePhoto: async (placeId: string) => {
    const hit = photoWait.get(placeId);
    if (hit) {
      return hit;
    }
    const pending = enqueuePlacePhoto(placeId);
    photoWait.set(placeId, pending);
    // Bulunamadıysa bir dakika sonra yeniden sorulabilsin: sunucu o sırada Google bütçesi ya da başka bir
    // kaynakla kareyi bulmuş olabilir. Bulunan kare kapak deposunda kalır.
    void pending.then((url) => {
      if (!url) {
        setTimeout(() => photoWait.delete(placeId), 60_000);
      }
    });
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
  /** Her zaman sunucudan tazeler; eşzamanlı çağrılar tek isteği paylaşır ve sonuç önbelleğe yazılır. */
  venue: (placeId: string) =>
    shareVenueRequest(placeId, async () =>
      mapVenueDetail(
        await request<Record<string, unknown>>(`/api/venues/${encodeURIComponent(placeId)}`, {}, { conditional: true }),
      ),
    ),
  /** Kart dokunulurken çağrılır: sayfa açılana kadar veri yolda olur. Hata sessizdir. */
  prefetchVenue: (placeId: string) => {
    if (!isFresh(placeId)) {
      void api.venue(placeId).catch(() => {});
    }
  },
  reviews: (placeId: string) => request<ReviewItem[]>(`/api/venues/${encodeURIComponent(placeId)}/reviews`),
  writeReview: (placeId: string, stars: number, text: string, tags: string[], photoUrls: string[] = []) =>
    request<ReviewItem>(`/api/venues/${encodeURIComponent(placeId)}/reviews`, {
      method: 'POST',
      body: JSON.stringify({ stars, text, tags, photoUrls }),
    }),
  deleteReview: (reviewId: string) => request(`/api/venues/reviews/${reviewId}`, { method: 'DELETE' }),
  myReviews: async () => {
    const data = await request<MyReview[] | undefined>('/api/me/reviews', {}, { optional: true });
    return data ?? [];
  },
  profile: () => request<Profile | undefined>('/api/me', {}, { optional: true }),
  updateProfile: (displayName: string) =>
    request<Profile>('/api/me', { method: 'PUT', body: JSON.stringify({ displayName }) }),
  changePassword: (currentPassword: string, newPassword: string) =>
    request<Session>('/api/auth/password', {
      method: 'POST',
      body: JSON.stringify({ currentPassword, newPassword }),
    }),
  consents: async () => {
    const data = await request<Record<string, boolean> | undefined>('/api/me/consents', {}, { optional: true });
    return data ?? {};
  },
  favorite: (placeId: string) =>
    request(`/api/venues/${encodeURIComponent(placeId)}/favorite`, { method: 'POST' }),
  unfavorite: (placeId: string) =>
    request(`/api/venues/${encodeURIComponent(placeId)}/favorite`, { method: 'DELETE' }),
  isFavorite: async (placeId: string) => {
    const data = await request<{ saved?: boolean } | undefined>(
      `/api/venues/${encodeURIComponent(placeId)}/favorite`,
      {},
      { optional: true },
    );
    return Boolean(data?.saved);
  },
  report: (placeId: string, reason: string, reviewId?: string, photoUrl?: string) =>
    request(`/api/venues/${encodeURIComponent(placeId)}/reports`, {
      method: 'POST',
      body: JSON.stringify({ reason, reviewId: reviewId ?? null, photoUrl: photoUrl ?? null }),
    }),
  blocks: async () => {
    const data = await request<{ userId: string; displayName: string }[] | undefined>('/api/me/blocks', {}, { optional: true });
    return data ?? [];
  },
  block: (userId: string) => request(`/api/me/blocks/${userId}`, { method: 'POST' }),
  unblock: (userId: string) => request(`/api/me/blocks/${userId}`, { method: 'DELETE' }),
  favorites: async () => {
    const data = await request<VenueCard[] | undefined>('/api/me/favorites', {}, { optional: true });
    return data ?? [];
  },
  search: (q: string, lat: number, lng: number) =>
    request<VenueCard[]>(`/api/search?q=${encodeURIComponent(q)}&lat=${lat}&lng=${lng}`).then((r) => r ?? []),
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
  // İşletme fotoğrafları önce, sonra sunucu galerisi; aynı Google karesinin farklı yazımları tek öğe olur.
  const photoUrls = venuePhotoUrls({
    ownerPhotos,
    gallery: Array.isArray(google.photoUrls) ? (google.photoUrls as string[]) : [],
    photoReference: typeof google.photoReference === 'string' ? google.photoReference : null,
  });
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
    summary: google.summary as string | undefined,
    priceLevel: google.priceLevel as number | undefined,
    category: google.category as string | undefined,
    externalReviews: Array.isArray(raw.externalReviews) ? (raw.externalReviews as VenueDetail['externalReviews']) : [],
    closesAt: (google.closesAt as string | null | undefined) ?? null,
    closesInMinutes: (google.closesInMinutes as number | null | undefined) ?? null,
    opensAt: (google.opensAt as string | null | undefined) ?? null,
    opensInMinutes: (google.opensInMinutes as number | null | undefined) ?? null,
  };
}
