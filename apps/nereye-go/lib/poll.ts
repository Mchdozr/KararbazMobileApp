// Arkadaşlarla karar ver: kısa liste, sonuçlar, paylaşım metni. React Native içe aktarmaz: node ile test edilir.
import { rankForDecision } from './decide.ts';
import type { VenueCard } from './types.ts';

export type PollOption = {
  placeId: string;
  name: string;
  category?: string | null;
  photoUrl?: string | null;
  latitude: number;
  longitude: number;
  votes: number;
};

export type Poll = {
  code: string;
  title: string;
  expiresAt: string;
  expired: boolean;
  options: PollOption[];
  totalVotes: number;
  myVote: string | null;
  winnerPlaceId: string | null;
  appUrl: string;
  webUrl: string | null;
};

export type PollOptionInput = Omit<PollOption, 'votes'>;

export const POLL_SIZE = 4;

/** "Benim için seç" sırasıyla ilk 4 mekan (reklam yok, kapalı olanlar geride). */
export function pollOptionsFrom(cards: readonly VenueCard[], size = POLL_SIZE): PollOptionInput[] {
  return rankForDecision(cards)
    .slice(0, size)
    .map((c) => ({
      placeId: c.placeId,
      name: c.name,
      category: c.category ?? null,
      photoUrl: c.photoUrl ?? null,
      latitude: c.latitude,
      longitude: c.longitude,
    }));
}

export type ResultRow = PollOption & { percent: number; mine: boolean; leading: boolean };

export function resultRows(poll: Poll): ResultRow[] {
  return poll.options.map((o) => ({
    ...o,
    percent: poll.totalVotes > 0 ? Math.round((o.votes / poll.totalVotes) * 100) : 0,
    mine: poll.myVote === o.placeId,
    leading: poll.winnerPlaceId === o.placeId,
  }));
}

export function pollShareText(poll: Poll): string {
  const lines = [`${poll.title} — oyunu ver:`, poll.options.map((o) => o.name).join(' · ')];
  if (poll.webUrl) {
    lines.push(poll.webUrl);
  }
  lines.push(`Uygulamada: ${poll.appUrl}`);
  return lines.join('\n');
}

export function timeLeft(expiresAt: string, now = Date.now()): string {
  const minutes = Math.floor((Date.parse(expiresAt) - now) / 60_000);
  if (minutes <= 0) {
    return 'Sona erdi';
  }
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h > 0 ? `${h} sa ${m} dk kaldı` : `${m} dk kaldı`;
}

export function hexKey(bytes: Uint8Array): string {
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}
