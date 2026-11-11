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
  intent?: string;
  cuisine?: string | null;
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
