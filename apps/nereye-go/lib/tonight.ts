// Bu akşam etkinlikleri: saat etiketleri ve sahip formu. Türkiye 2016'dan beri sabit UTC+3 kullanır.
// React Native içe aktarmaz: node ile test edilir.
import { dativeTime } from './open-status.ts';

const IST_OFFSET_MS = 3 * 60 * 60 * 1000;

function istanbulParts(ms: number) {
  const d = new Date(ms + IST_OFFSET_MS);
  return { y: d.getUTCFullYear(), mo: d.getUTCMonth(), d: d.getUTCDate(), h: d.getUTCHours(), m: d.getUTCMinutes() };
}

const pad = (n: number) => String(n).padStart(2, '0');

function clock(ms: number) {
  const p = istanbulParts(ms);
  return `${pad(p.h)}:${pad(p.m)}`;
}

/** "20:30", "Gece 01:00" (gece yarısından sonra), sürüyorsa "Şu an · 22:00’a kadar". */
export function eventTimeLabel(startsAt: string, endsAt: string | null | undefined, live: boolean, now = Date.now()): string {
  if (live) {
    return endsAt ? `Şu an · ${dativeTime(clock(Date.parse(endsAt)))} kadar` : 'Şu an sürüyor';
  }
  const start = Date.parse(startsAt);
  const s = istanbulParts(start);
  const n = istanbulParts(now);
  const nextDay = s.d !== n.d || s.mo !== n.mo;
  return nextDay && s.h < 6 ? `Gece ${clock(start)}` : clock(start);
}

export function parseClock(text: string): { h: number; m: number } | null {
  const match = /^\s*(\d{1,2})[:.](\d{2})\s*$/.exec(text);
  if (!match) {
    return null;
  }
  const h = Number(match[1]);
  const m = Number(match[2]);
  return h <= 23 && m <= 59 ? { h, m } : null;
}

/**
 * Sahip formu: "bugünden kaç gün sonra" + İstanbul saati → UTC ISO. Gece yarısından sonraki saat (ör. 01:00)
 * bir sonraki takvim gününe düşer: "Bugün 01:00" bu gecenin 01:00'idir.
 */
export function eventStartIso(dayOffset: number, hhmm: string, now = Date.now()): string | null {
  const t = parseClock(hhmm);
  if (!t) {
    return null;
  }
  const n = istanbulParts(now);
  const afterMidnight = t.h < 6 ? 1 : 0;
  const localMs = Date.UTC(n.y, n.mo, n.d + dayOffset + afterMidnight, t.h, t.m);
  return new Date(localMs - IST_OFFSET_MS).toISOString();
}

type OwnerEvent = { id: string; title: string; startsAt: string; endsAt?: string | null };

/** Bitmemiş etkinlikler, başlama saatine göre. Bitişi yoksa başladıktan 2 saat sonra biter sayılır (sunucuyla aynı). */
export function upcomingEvents<T extends OwnerEvent>(events: readonly T[], now = Date.now()): T[] {
  return events
    .filter((e) => (e.endsAt ? Date.parse(e.endsAt) : Date.parse(e.startsAt) + 2 * 3_600_000) > now)
    .sort((a, b) => Date.parse(a.startsAt) - Date.parse(b.startsAt));
}

/** "Bugün 20:30", "Yarın 20:00", "26.11 20:00" (İstanbul saati). */
export function eventDayLabel(startsAt: string, now = Date.now()): string {
  const ms = Date.parse(startsAt);
  const s = istanbulParts(ms);
  const n = istanbulParts(now);
  const days = Math.round((Date.UTC(s.y, s.mo, s.d) - Date.UTC(n.y, n.mo, n.d)) / 86_400_000);
  const day = days === 0 ? 'Bugün' : days === 1 ? 'Yarın' : `${pad(s.d)}.${pad(s.mo + 1)}`;
  return `${day} ${clock(ms)}`;
}
