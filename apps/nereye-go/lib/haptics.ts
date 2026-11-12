import * as Haptics from 'expo-haptics';
import { Platform } from 'react-native';

const on = Platform.OS === 'ios' || Platform.OS === 'android';

/** Dokunsal geri bildirim. Desteklenmeyen cihazda sessizce geçer. */
export const haptic = {
  tap() {
    if (on) {
      void Haptics.selectionAsync().catch(() => {});
    }
  },
  press() {
    if (on) {
      void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light).catch(() => {});
    }
  },
  success() {
    if (on) {
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
    }
  },
  warn() {
    if (on) {
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning).catch(() => {});
    }
  },
};
