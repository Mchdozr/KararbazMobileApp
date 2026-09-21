import * as SecureStore from 'expo-secure-store';

const KEY = 'nereye.onboardedAt';

export async function getOnboardedAt() {
  try {
    return await SecureStore.getItemAsync(KEY);
  } catch {
    return null;
  }
}

export async function markOnboarded() {
  try {
    await SecureStore.setItemAsync(KEY, new Date().toISOString());
  } catch {
    /* web */
  }
}
