const INTENT_TR: Record<string, string> = {
  Kahve: 'kahve',
  Yemek: 'yemek',
  Eglence: 'eğlence',
  Kultur: 'kültür',
  AcikHava: 'açık hava',
  Gece: 'gece',
};

export function emptyNearby(intent: string, label: string) {
  const name = INTENT_TR[intent] ?? intent.toLowerCase();
  return `Bu civarda ${name} yok. ${label} için yarıçapı büyüt veya niyeti değiştir.`;
}

export function emptySearch(q: string) {
  return `“${q}” için sonuç yok. Mahalle veya mekan adını dene.`;
}

export function emptyMap(intent: string) {
  const name = INTENT_TR[intent] ?? intent.toLowerCase();
  return `Haritada ${name} görünmüyor. Kaydır veya niyeti değiştir.`;
}

export function emptyDiscover(intent: string) {
  const name = INTENT_TR[intent] ?? intent.toLowerCase();
  return `Bu mesafede ${name} yok. Filtreyi gevşet.`;
}

export function friendlyError(reason: unknown) {
  const raw = reason instanceof Error ? reason.message : typeof reason === 'string' ? reason : '';
  if (!raw.trim()) {
    return 'Bir şey ters gitti. Tekrar dene.';
  }
  const lower = raw.toLowerCase();
  if (lower.includes('network') || lower.includes('failed to fetch') || lower.includes('timeout')) {
    return 'Ağa ulaşılamadı. Wi-Fi ve API adresini kontrol et.';
  }
  if (lower.includes('429') || lower.includes('too many')) {
    return 'Çok sık arama. Bir dakika sonra tekrar dene.';
  }
  if (lower.includes('401') || lower.includes('unauthorized')) {
    return 'Oturum doldu. Giriş yapman yeterli.';
  }
  if (raw.length > 140 || raw.startsWith('{') || raw.startsWith('<')) {
    return 'Sunucu cevap vermedi. Tekrar dene.';
  }
  return raw;
}
