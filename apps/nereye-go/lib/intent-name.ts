// Niyet adı (saf; lib/constants.ts RN içe aktardığı için node testlerinde bu kullanılır).
import { t } from './i18n.ts';

const NAMES: Record<string, string> = {
  Kahve: 'Kahve',
  Yemek: 'Yemek',
  Eglence: 'Eğlence',
  Kultur: 'Kültür',
  AcikHava: 'Açık hava',
  Gece: 'Gece',
};

export function intentName(intent: string): string {
  return NAMES[intent] ? t(NAMES[intent]) : intent;
}
