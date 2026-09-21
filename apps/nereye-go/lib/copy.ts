export function timeGreeting() {
  const h = new Date().getHours();
  if (h < 5) {
    return 'Gece yarısı nereye?';
  }
  if (h < 12) {
    return 'Günaydın, nereye gidelim?';
  }
  if (h < 17) {
    return 'İyi günler — bir mola?';
  }
  if (h < 21) {
    return 'İyi akşamlar, bu akşam nereye?';
  }
  return 'Gece için sakin bir yer?';
}

export function isEvening() {
  const h = new Date().getHours();
  return h >= 19 || h < 5;
}

const DEFAULT_NOTES = ['Sizin için bakıyoruz…', 'Yakın masaları tarıyoruz…', 'Bir saniye, neredeyse hazır.'];

export const SEARCH_NOTES: Record<string, string[]> = {
  Kahve: ['Sizin için bakıyoruz…', 'Taze çekim izini sürüyoruz…', 'Köşe masaları tarıyoruz…', 'Fincan ısınsın diye bakıyoruz.'],
  Yemek: ['Mutfakları yokluyoruz…', 'Sofra mesafesini ölçüyoruz…', 'Sizin için bakıyoruz…'],
  Eglence: ['Sahne ışıklarını tarıyoruz…', 'Bu gece nereye, bakıyoruz…'],
  Kultur: ['Salonları yokluyoruz…', 'Bir biletlik yer arıyoruz…'],
  AcikHava: ['Açık havayı kokluyoruz…', 'Çimen, rüzgâr, bakıyoruz…'],
  Gece: ['Gece açık yerleri tarıyoruz…', 'Işıkları henüz sönmeyenleri arıyoruz…'],
};

export function searchNotes(intent: string) {
  return SEARCH_NOTES[intent] ?? DEFAULT_NOTES;
}

export function noteFor(intent: string, tick: number) {
  const list = searchNotes(intent);
  return list[tick % list.length];
}

export const QUICK_PLACES = [
  { label: 'Kadıköy', lat: 40.9885, lng: 29.025 },
  { label: 'Moda', lat: 40.9808, lng: 29.0261 },
  { label: 'Nişantaşı', lat: 41.0489, lng: 28.994 },
  { label: 'Halide Edip Adıvar', lat: 41.055, lng: 28.987 },
  { label: 'Osmanbey', lat: 41.0524, lng: 28.9878 },
  { label: 'Karaköy', lat: 41.0234, lng: 28.973 },
  { label: 'Cihangir', lat: 41.0324, lng: 28.9865 },
  { label: 'Beşiktaş', lat: 41.0422, lng: 29.0078 },
] as const;
