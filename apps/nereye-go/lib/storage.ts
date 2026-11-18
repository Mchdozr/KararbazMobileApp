import { File, Paths } from 'expo-file-system';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

/**
 * Küçük anahtar-değer deposu. Native'de SecureStore (oturum token'ı da burada durur),
 * web'de localStorage. Hiçbir çağrı fırlatmaz: depolama yoksa uygulama yine çalışır.
 */
const web = Platform.OS === 'web';

function local(): Storage | null {
  try {
    return typeof globalThis.localStorage === 'undefined' ? null : globalThis.localStorage;
  } catch {
    return null;
  }
}

export async function getItem(key: string): Promise<string | null> {
  try {
    return web ? (local()?.getItem(key) ?? null) : await SecureStore.getItemAsync(key);
  } catch {
    return null;
  }
}

export async function setItem(key: string, value: string): Promise<void> {
  try {
    if (web) {
      local()?.setItem(key, value);
    } else {
      await SecureStore.setItemAsync(key, value);
    }
  } catch {
    /* depolama dolu ya da kapalı */
  }
}

export async function removeItem(key: string): Promise<void> {
  try {
    if (web) {
      local()?.removeItem(key);
    } else {
      await SecureStore.deleteItemAsync(key);
    }
  } catch {
    /* yok say */
  }
}

export async function getJson<T>(key: string): Promise<T | null> {
  const raw = await getItem(key);
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

export function setJson(key: string, value: unknown) {
  return setItem(key, JSON.stringify(value));
}

/**
 * Büyük ve gizli olmayan veri (ör. son öneri listesi). SecureStore değer başına ~2 KB ile sınırlıdır;
 * bu yüzden native'de uygulamanın belge klasöründe dosya, web'de localStorage kullanılır. Hiç fırlatmaz.
 * Oturum token'ı gibi sırlar buraya yazılmaz.
 */
export async function getLargeJson<T>(name: string): Promise<T | null> {
  try {
    const raw = web ? (local()?.getItem(`large:${name}`) ?? null) : await readFile(name);
    return raw ? (JSON.parse(raw) as T) : null;
  } catch {
    return null;
  }
}

export async function setLargeJson(name: string, value: unknown): Promise<void> {
  try {
    const raw = JSON.stringify(value);
    if (web) {
      local()?.setItem(`large:${name}`, raw);
      return;
    }
    const file = new File(Paths.document, `${name}.json`);
    if (!file.exists) {
      file.create();
    }
    await file.write(raw);
  } catch {
    /* disk dolu ya da kapalı: yalnızca önbellek, yok say */
  }
}

async function readFile(name: string): Promise<string | null> {
  const file = new File(Paths.document, `${name}.json`);
  return file.exists ? await file.text() : null;
}
