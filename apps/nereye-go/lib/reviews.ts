import type { ReviewItem } from '@/lib/types';

export const REVIEW_MAX = 1000;
export const REVIEW_MAX_PHOTOS = 4;

export const STAR_LABELS = ['', 'Kötü', 'Zayıf', 'İdare eder', 'İyi', 'Harika'] as const;

export type RatingSummary = {
  count: number;
  average: number | null;
  /** index 0 = 1 yıldız … index 4 = 5 yıldız */
  buckets: [number, number, number, number, number];
};

export function summarizeRatings(reviews: Pick<ReviewItem, 'stars'>[]): RatingSummary {
  const buckets: RatingSummary['buckets'] = [0, 0, 0, 0, 0];
  let sum = 0;
  let count = 0;
  for (const r of reviews) {
    const s = Math.round(r.stars);
    if (s >= 1 && s <= 5) {
      buckets[s - 1] += 1;
      sum += s;
      count += 1;
    }
  }
  return { count, average: count ? sum / count : null, buckets };
}

/** "az önce", "3 saat önce", "dün", "12 gün önce", sonra tarih. */
export function relativeDate(iso?: string | null, now: Date = new Date()): string {
  if (!iso) {
    return '';
  }
  const then = new Date(iso);
  const ms = now.getTime() - then.getTime();
  if (Number.isNaN(ms)) {
    return '';
  }
  const min = Math.floor(ms / 60_000);
  if (min < 1) {
    return 'az önce';
  }
  if (min < 60) {
    return `${min} dk önce`;
  }
  const hours = Math.floor(min / 60);
  if (hours < 24) {
    return `${hours} saat önce`;
  }
  const days = Math.floor(hours / 24);
  if (days === 1) {
    return 'dün';
  }
  if (days < 30) {
    return `${days} gün önce`;
  }
  const months = Math.floor(days / 30);
  if (months < 12) {
    return `${months} ay önce`;
  }
  return `${Math.floor(days / 365)} yıl önce`;
}
