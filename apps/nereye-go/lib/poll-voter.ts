import { hexKey } from '@/lib/poll';
import { getItem, setItem } from '@/lib/storage';

const KEY = 'poll.voter';
let cached: string | null = null;

/** Bu cihazın oy anahtarı: rastgele, hesaba bağlı değil. Sunucu yalnızca özetini saklar. */
export async function voterKey(): Promise<string> {
  if (cached) {
    return cached;
  }
  const saved = await getItem(KEY);
  if (saved && /^[0-9a-f]{32}$/.test(saved)) {
    cached = saved;
    return saved;
  }
  const bytes = new Uint8Array(16);
  const c = (globalThis as { crypto?: { getRandomValues?: (a: Uint8Array) => Uint8Array } }).crypto;
  if (c?.getRandomValues) {
    c.getRandomValues(bytes);
  } else {
    for (let i = 0; i < bytes.length; i++) {
      bytes[i] = Math.floor(Math.random() * 256);
    }
  }
  cached = hexKey(bytes);
  await setItem(KEY, cached);
  return cached;
}
