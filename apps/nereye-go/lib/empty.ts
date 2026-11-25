import { t } from './i18n.ts';

const INTENT_TR: Record<string, string> = {
  Kahve: 'kahve',
  Yemek: 'yemek',
  Eglence: 'eğlence',
  Kultur: 'kültür',
  AcikHava: 'açık hava',
  Gece: 'gece',
};

function intentName(intent: string) {
  return t(INTENT_TR[intent] ?? intent.toLowerCase());
}

export function emptyNearby(intent: string, label: string) {
  return t('Bu civarda {name} yok. {label} için yarıçapı büyüt veya niyeti değiştir.', { name: intentName(intent), label });
}

export function emptySearch(q: string) {
  return t('“{q}” için sonuç yok. Mahalle veya mekan adını dene.', { q });
}

export function emptyMap(intent: string) {
  return t('Haritada {name} görünmüyor. Kaydır veya niyeti değiştir.', { name: intentName(intent) });
}

export function emptyDiscover(intent: string) {
  return t('Bu mesafede {name} yok. Filtreyi gevşet.', { name: intentName(intent) });
}

const NETWORK = 'Ağa ulaşılamadı. Bağlantını kontrol edip tekrar dene.';

export function friendlyError(reason: unknown) {
  // ApiError'a yapısal bakıyoruz (instanceof değil): bu dosya saf kalsın, test edilebilsin.
  const api = reason as { name?: string; status?: number; kind?: string; message?: string } | null;
  if (api && api.name === 'ApiError') {
    if (api.kind === 'timeout') {
      return t('Sunucu geç yanıt veriyor. Tekrar dene.');
    }
    if (api.kind === 'network') {
      return t(NETWORK);
    }
    if (api.kind === 'aborted') {
      // Ekran isteği kendisi bıraktı; normalde gösterilmez, gösterilirse de ham metin çıkmasın.
      return t('İstek yarıda bırakıldı.');
    }
    if (api.status === 429) {
      return api.message || t('Çok sık istek. Bir dakika sonra tekrar dene.');
    }
    if (api.status === 401) {
      return api.message || t('Oturum doldu. Giriş yapman yeterli.');
    }
    if (api.status === 404 && !api.message) {
      return t('Aradığın yer bulunamadı. Kaldırılmış ya da taşınmış olabilir.');
    }
    if (api.status === 403 && !api.message) {
      return t('Bu işlem için yetkin yok.');
    }
    // 503: sunucu bir özelliği bilerek kapattı ("Şifre sıfırlama şu an kapalı."); Türkçe nedeni göster.
    if (api.status === 503 && api.message && api.message.length <= 160) {
      return api.message;
    }
    if ((api.status ?? 0) >= 500) {
      return t('Sunucuda bir sorun var. Biraz sonra tekrar dene.');
    }
    if (api.message && api.message.length <= 160) {
      return api.message;
    }
    return t('İstek tamamlanamadı. Tekrar dene.');
  }

  const raw = reason instanceof Error ? reason.message : typeof reason === 'string' ? reason : '';
  if (!raw.trim()) {
    return t('Bir şey ters gitti. Tekrar dene.');
  }
  const lower = raw.toLowerCase();
  if (
    lower.includes('network')
    || lower.includes('failed to fetch')
    || lower.includes('fetch failed')
    || lower.includes('couldnt connect')
    || lower.includes("couldn't connect")
    || lower.includes('unexpectedexception')
    || lower.includes('timeout')
    || lower.includes('aborted')
  ) {
    return t(NETWORK);
  }
  if (lower.includes('429') || lower.includes('too many')) {
    return t('Çok sık arama. Bir dakika sonra tekrar dene.');
  }
  if (lower.includes('401') || lower.includes('unauthorized')) {
    return t('Oturum doldu. Giriş yapman yeterli.');
  }
  if (raw.length > 140 || raw.startsWith('{') || raw.startsWith('<')) {
    return t('Sunucu cevap vermedi. Tekrar dene.');
  }
  return raw;
}
