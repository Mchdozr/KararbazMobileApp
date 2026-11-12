import { getItem, removeItem, setItem } from '@/lib/storage';

const KEY = 'nereye.onboardedAt';

export async function getOnboardedAt() {
  try {
    return await getItem(KEY);
  } catch {
    return null;
  }
}

export async function markOnboarded() {
  try {
    await setItem(KEY, new Date().toISOString());
  } catch {
    /* web */
  }
}
