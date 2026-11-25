import { api } from '@/lib/api';
import { routeFromUrl, type PushRoute } from '@/lib/push-route';
import { getItem, removeItem, setItem } from '@/lib/storage';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

/**
 * Etkinlik hatırlatmaları (açık rızayla). Kullanıcı Profil'de açınca sistem izni istenir, Expo belirteci sunucuya
 * kaydedilir; kapatınca belirteç silinir. Web'de bildirim yok.
 */
export const pushSupported = Platform.OS !== 'web';
const KEY = 'push.token';

export async function pushToken(): Promise<string | null> {
  return pushSupported ? getItem(KEY) : null;
}

export type EnableResult = 'ok' | 'denied' | 'unsupported' | 'error';

export async function enablePush(): Promise<EnableResult> {
  if (!pushSupported) {
    return 'unsupported';
  }
  try {
    const current = await Notifications.getPermissionsAsync();
    const granted = current.granted || (await Notifications.requestPermissionsAsync()).granted;
    if (!granted) {
      return 'denied';
    }
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync('events', {
        name: 'Etkinlik hatırlatmaları',
        importance: Notifications.AndroidImportance.DEFAULT,
      });
    }
    // projectId app.json'daki extra.eas.projectId'den okunur.
    const { data: token } = await Notifications.getExpoPushTokenAsync();
    await api.registerPush(token, Platform.OS);
    await setItem(KEY, token);
    return 'ok';
  } catch {
    return 'error';
  }
}

export async function disablePush(): Promise<void> {
  const token = await getItem(KEY);
  await api.unregisterPush(token);
  await removeItem(KEY);
}

let installed = false;

/** Ön planda gelen bildirim de gösterilsin; dokununca ilgili ekrana gidilsin (soğuk açılış dahil). */
export function installPushHandlers(router: { push: (route: PushRoute) => void }) {
  if (!pushSupported || installed) {
    return () => {};
  }
  installed = true;
  Notifications.setNotificationHandler({
    handleNotification: async () => ({ shouldShowBanner: true, shouldShowList: true, shouldPlaySound: true, shouldSetBadge: false }),
  });
  const open = (url: unknown) => {
    const route = routeFromUrl(url);
    if (route) {
      router.push(route);
    }
  };
  const last = Notifications.getLastNotificationResponse();
  if (last) {
    open(last.notification.request.content.data?.url);
  }
  const sub = Notifications.addNotificationResponseReceivedListener((r) => open(r.notification.request.content.data?.url));
  return () => {
    sub.remove();
    installed = false;
  };
}
