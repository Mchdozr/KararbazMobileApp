export type Session = {
  accessToken: string;
  refreshToken: string;
  userId: string;
  displayName: string;
  roles: string[];
  avatarUrl?: string | null;
};

export type SavedPlace = {
  id: string;
  label: string;
  kind: string;
  latitude: number;
  longitude: number;
};

export type VenueCard = {
  placeId: string;
  name: string;
  address?: string | null;
  distanceMeters: number;
  kind: string;
  openNow?: boolean | null;
  nereyeRating?: number | null;
  nereyeRatingCount: number;
  photoUrl?: string | null;
  photoReference?: string | null;
  vibes: string[];
  upcomingEvents: { id: string; title: string; startsAt: string }[];
  latitude: number;
  longitude: number;
  note?: string | null;
  googleRating?: number | null;
  googleRatingCount?: number | null;
  /** 0 ücretsiz … 4 çok pahalı */
  priceLevel?: number | null;
  category?: string | null;
  /** Sunucu İstanbul saatiyle hesaplar: açıkken kapanış, kapalıyken sonraki açılış. */
  closesAt?: string | null;
  closesInMinutes?: number | null;
  opensAt?: string | null;
  opensInMinutes?: number | null;
};

/** Dış kaynaktan (Google) gelen yorum. */
export type ExternalReview = {
  author: string;
  rating: number;
  text?: string | null;
  when?: string | null;
  authorPhotoUrl?: string | null;
  /** Yazarın Google profili; yazar adı buna bağlanır. */
  authorUri?: string | null;
};

export type VenueDetail = {
  placeId: string;
  name: string;
  address?: string | null;
  openNow?: boolean | null;
  googleRating?: number | null;
  googleRatingCount?: number | null;
  nereyeRating?: number | null;
  nereyeRatingCount: number;
  claimed: boolean;
  mapsUri?: string | null;
  ownerDescription?: string | null;
  attribution: string;
  vibes: string[];
  events: { id: string; title: string; startsAt: string }[];
  latitude: number;
  longitude: number;
  photoUrl?: string | null;
  photoUrls: string[];
  note?: string | null;
  phone?: string | null;
  website?: string | null;
  openingHours?: string | null;
  isFavorite?: boolean;
  geohash?: string | null;
  cuisine?: string | null;
  outdoorSeating?: string | null;
  internetAccess?: string | null;
  amenityChips?: string[];
  summary?: string | null;
  priceLevel?: number | null;
  category?: string | null;
  externalReviews?: ExternalReview[];
  closesAt?: string | null;
  closesInMinutes?: number | null;
  opensAt?: string | null;
  opensInMinutes?: number | null;
};

export type RouteMode = 'foot' | 'driving';

export type RouteResult = {
  mode: string;
  distanceMeters: number;
  durationSeconds: number;
  points: { latitude: number; longitude: number }[];
};

export type ReviewItem = {
  id: string;
  userId?: string;
  authorAvatarUrl?: string | null;
  stars: number;
  text?: string | null;
  ownerReply?: string | null;
  authorName?: string | null;
  createdAt?: string | null;
  tags?: string[];
  photoUrls?: string[];
};

export type Preferences = {
  defaultRadiusMeters: number;
  intents: string[];
  vibes: string[];
};

export type PromotionItem = {
  id: string;
  placeId: string;
  targetIntent: string;
  targetGeohash: string;
  startAt: string;
  endAt: string;
  status: string;
  dailyBudget: number;
};

export type MyReview = {
  id: string;
  placeId: string;
  venueName: string;
  stars: number;
  text?: string | null;
  tags: string[];
  photoUrls: string[];
  ownerReply?: string | null;
  createdAt: string;
};

export type Profile = {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  roles: string[];
  createdAt: string;
  reviewCount: number;
  favoriteCount: number;
  savedPlaceCount: number;
  /** Yorum ve fotoğraf için e-posta doğrulaması şart. */
  emailVerified?: boolean;
};

export type LegalSection = { title: string; body: string };
export type LegalDocument = { title: string; lead: string; sections: LegalSection[] };
export type Legal = { privacy: LegalDocument; terms: LegalDocument; contact: string | null; draft: string };
