// Açık/kapalı satırı: "23:00'e kadar açık", "Yakında kapanıyor · 23:00", "Yarın 09:00'da açılıyor".
// Saat dilimi hesabı sunucuda (İstanbul) yapılır; burada yalnızca metin kurulur. React Native içe aktarmaz: node ile test edilir.
import { language, t } from './i18n.ts';

export type OpenInfo = {
  openNow?: boolean | null;
  closesAt?: string | null;
  closesInMinutes?: number | null;
  opensAt?: string | null;
  opensInMinutes?: number | null;
};

export type OpenTone = 'open' | 'soon' | 'closed';
export type OpenLine = { text: string; tone: OpenTone; short: string };

/** Kapanışa bu kadar dakika kala "Yakında kapanıyor" denir. */
export const CLOSING_SOON_MINUTES = 45;

const UNITS = ['sıfır', 'bir', 'iki', 'üç', 'dört', 'beş', 'altı', 'yedi', 'sekiz', 'dokuz'];
const TENS: Record<number, string> = { 1: 'on', 2: 'yirmi', 3: 'otuz', 4: 'kırk', 5: 'elli' };
const BACK = new Set(['a', 'ı', 'o', 'u']);
const VOWELS = new Set(['a', 'ı', 'o', 'u', 'e', 'i', 'ö', 'ü']);
const HARD = new Set(['ç', 'f', 'h', 'k', 'p', 's', 'ş', 't']);

/** Saatin okunuşundaki son kelime: "23:00" -> "üç", "23:30" -> "otuz", "00:00" -> "sıfır". */
function spokenLast(hhmm: string): string | null {
  const m = /^(\d{1,2}):(\d{2})$/.exec(hhmm.trim());
  if (!m) {
    return null;
  }
  const hour = Number(m[1]);
  const minute = Number(m[2]);
  const n = minute === 0 ? hour % 24 : minute;
  if (n === 0) {
    return 'sıfır';
  }
  const unit = n % 10;
  return unit > 0 ? UNITS[unit] : TENS[Math.floor(n / 10)] ?? null;
}

function lastVowel(word: string) {
  for (let i = word.length - 1; i >= 0; i--) {
    if (VOWELS.has(word[i])) {
      return word[i];
    }
  }
  return 'e';
}

/** Yönelme eki: "23:00'e", "22:00'ye", "06:00'ya", "00:00'a". */
export function dativeTime(hhmm: string): string {
  const word = spokenLast(hhmm);
  if (!word) {
    return hhmm;
  }
  const back = BACK.has(lastVowel(word));
  const buffer = VOWELS.has(word[word.length - 1]) ? 'y' : '';
  return `${hhmm}'${buffer}${back ? 'a' : 'e'}`;
}

/** Bulunma eki: "09:00'da", "08:00'de", "04:00'te", "17:40'ta". */
export function locativeTime(hhmm: string): string {
  const word = spokenLast(hhmm);
  if (!word) {
    return hhmm;
  }
  const back = BACK.has(lastVowel(word));
  const hard = HARD.has(word[word.length - 1]);
  return `${hhmm}'${hard ? 't' : 'd'}${back ? 'a' : 'e'}`;
}

/**
 * Kart ve mekan sayfası için satır. nowMinutes: cihazın günün kaçıncı dakikasında olduğu
 * (yalnızca "bugün mü, yarın mı" ayrımı için).
 */
export function openLine(info: OpenInfo, nowMinutes: number): OpenLine | null {
  if (info.openNow === true) {
    const left = info.closesInMinutes;
    if (info.closesAt && left != null && left <= CLOSING_SOON_MINUTES) {
      return { text: t('Yakında kapanıyor · {time}', { time: info.closesAt }), tone: 'soon', short: t('Kapanıyor') };
    }
    if (info.closesAt) {
      const text = language() === 'en' ? t('{time} kadar açık', { time: info.closesAt }) : `${dativeTime(info.closesAt)} kadar açık`;
      return { text, tone: 'open', short: t('Açık') };
    }
    return { text: t('Şu an açık'), tone: 'open', short: t('Açık') };
  }
  if (info.openNow === false) {
    const wait = info.opensInMinutes;
    if (info.opensAt && wait != null && wait > 0) {
      if (wait <= 60) {
        return { text: t('{n} dk sonra açılıyor', { n: wait }), tone: 'closed', short: t('Kapalı') };
      }
      const at = nowMinutes + wait;
      if (at < 24 * 60) {
        const text = language() === 'en' ? t('{time} açılıyor', { time: info.opensAt }) : `${locativeTime(info.opensAt)} açılıyor`;
        return { text, tone: 'closed', short: t('Kapalı') };
      }
      if (at < 48 * 60) {
        const text = language() === 'en' ? t('Yarın {time} açılıyor', { time: info.opensAt }) : `Yarın ${locativeTime(info.opensAt)} açılıyor`;
        return { text, tone: 'closed', short: t('Kapalı') };
      }
    }
    return { text: t('Şu an kapalı'), tone: 'closed', short: t('Kapalı') };
  }
  return null;
}

/** Cihaz saatine göre günün dakikası. */
export function minutesOfDay(d: Date = new Date()): number {
  return d.getHours() * 60 + d.getMinutes();
}
