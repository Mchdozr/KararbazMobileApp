// Arayüz dili. Anahtar Türkçe metnin kendisidir: t('Keşfet') Türkçede olduğu gibi döner, İngilizcede
// lib/locales/en.ts'deki karşılığı. {ad} biçimindeki yer tutucular doldurulur. Sözlükte olmayan metin
// Türkçe kalır; lib/i18n.test.ts kaynakta t() ile sarılan her metnin İngilizcesi olduğunu denetler.
// React Native içe aktarmaz: node ile test edilir.
import { en } from './locales/en.ts';

export type Lang = 'tr' | 'en';
export type LangPref = Lang | 'system';

let current: Lang = 'tr';

export function setLanguage(lang: Lang) {
  current = lang;
}

export function language(): Lang {
  return current;
}

export function t(text: string, params?: Record<string, string | number>): string {
  // "Ara|telefon": aynı Türkçe sözcüğün farklı anlamı. Türkçede "|" sonrası atılır.
  const bar = text.indexOf('|');
  let s = current === 'en' ? (en[text] ?? (bar >= 0 ? text.slice(0, bar) : text)) : bar >= 0 ? text.slice(0, bar) : text;
  if (params) {
    for (const [k, v] of Object.entries(params)) {
      s = s.split(`{${k}}`).join(String(v));
    }
  }
  return s;
}

/** Tercih (Profil) cihaz dilini ezer; "sistem" ise cihaz Türkçe değilse İngilizce. */
export function resolveLanguage(pref: string | null | undefined, deviceLanguage: string | null | undefined): Lang {
  if (pref === 'tr' || pref === 'en') {
    return pref;
  }
  return (deviceLanguage ?? 'tr').toLowerCase().startsWith('tr') ? 'tr' : 'en';
}

/** Ondalık: Türkçede virgül (4,6), İngilizcede nokta (4.6). */
export function decimal(n: number, digits = 1): string {
  const s = n.toFixed(digits);
  return current === 'tr' ? s.replace('.', ',') : s;
}
