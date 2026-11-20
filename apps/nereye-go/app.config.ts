import type { ConfigContext, ExpoConfig } from 'expo/config';

/**
 * Derleme varyantı. EAS derlemede `EAS_BUILD_PROFILE` profil adını taşır; yerelde `APP_VARIANT` ile
 * zorlanabilir. İkisi de yoksa (expo start, yerel prebuild) geliştirme varsayılır.
 */
export type Variant = 'development' | 'preview' | 'production';

export function variantOf(env: Record<string, string | undefined>): Variant {
  const name = (env.APP_VARIANT || env.EAS_BUILD_PROFILE || 'development').trim().toLowerCase();
  if (name === 'production') {
    return 'production';
  }
  return name === 'development' ? 'development' : 'preview';
}

/**
 * Yayına çıkan bir paket (production ya da preview) yalnızca insanın verdiği https adresle derlenir.
 * Adres yoksa ya da https değilse derleme burada durur; uygulama asla kapalı bir tünele,
 * LAN adresine ya da düz HTTP'ye derlenmez.
 */
export function assertReleaseApiUrl(variant: Variant, url: string | undefined): void {
  if (variant === 'development') {
    return;
  }
  const value = url?.trim() ?? '';
  if (!value) {
    throw new Error(
      `[nereye] ${variant} derlemesi için EXPO_PUBLIC_API_URL tanımlı değil. ` +
        'EAS ortam değişkeni olarak kalıcı, https ile başlayan API adresini ver.',
    );
  }
  if (!/^https:\/\/[^\s/]+/i.test(value)) {
    throw new Error(`[nereye] ${variant} derlemesinde EXPO_PUBLIC_API_URL https ile başlamalı (verilen: ${value}).`);
  }
}

export function buildConfig(config: ExpoConfig, env: Record<string, string | undefined>): ExpoConfig {
  const variant = variantOf(env);
  assertReleaseApiUrl(variant, env.EXPO_PUBLIC_API_URL);
  const dev = variant === 'development';

  const infoPlist = { ...(config.ios?.infoPlist ?? {}) };
  if (dev) {
    // Yalnızca geliştirmede: telefondan bilgisayardaki API'ye düz HTTP ile bağlanabilmek için.
    infoPlist.NSAppTransportSecurity = { NSAllowsArbitraryLoads: true };
  } else {
    delete infoPlist.NSAppTransportSecurity;
  }

  // SDK 58'de cleartext ayarı üst düzey `android` alanından kalktı; expo-build-properties üzerinden verilir.
  const plugins = (config.plugins ?? []).filter((p) => (Array.isArray(p) ? p[0] : p) !== 'expo-build-properties');
  plugins.push([
    'expo-build-properties',
    {
      android: {
        // Yalnızca geliştirme varyantı yerel ağdaki API'ye düz HTTP ile bağlanabilir.
        usesCleartextTraffic: dev,
        // Play'in güncel hedef API şartı: docs/yayin-riskleri.md. RN 0.88 varsayılanıyla aynı; düşürülmesin.
        targetSdkVersion: TARGET_SDK,
      },
    },
  ]);

  // Google Maps Android anahtarı yalnızca derleme ortamından (EAS secret) gelir; repoya ve `extra`ya yazılmaz.
  // Anahtar yoksa uygulama haritayı çizmez ("harita anahtarı yok"); public OSM döşemesine düşmez.
  const mapsKey = env.GOOGLE_MAPS_ANDROID_API_KEY?.trim();
  if (variant === 'production' && !mapsKey) {
    // Anahtarsız Android derlemesinde harita boş kalır; mağazaya böyle bir paket çıkmasın.
    throw new Error('[nereye] production derlemesi için GOOGLE_MAPS_ANDROID_API_KEY (EAS ortam değişkeni) gerekli; yoksa harita görünmez.');
  }
  const android = { ...config.android };
  if (mapsKey) {
    android.config = { ...android.config, googleMaps: { apiKey: mapsKey } };
  }

  // iOS: anahtar verilirse Google haritası, verilmezse Apple Haritalar (anahtarsız, her zaman çalışır).
  const iosMapsKey = env.GOOGLE_MAPS_IOS_API_KEY?.trim();
  const ios = { ...config.ios, infoPlist };
  if (iosMapsKey) {
    ios.config = { ...ios.config, googleMapsApiKey: iosMapsKey };
  }

  return {
    ...config,
    ios,
    android,
    plugins,
    extra: {
      ...config.extra,
      variant,
      googleMapsEnabled: Boolean(mapsKey),
      googleMapsIosEnabled: Boolean(iosMapsKey),
    },
  };
}

export const TARGET_SDK = 36;

/** Derlenmiş yapılandırmada Android cleartext ayarı (testler ve denetim için). */
export function cleartextOf(config: ExpoConfig): boolean | undefined {
  const entry = (config.plugins ?? []).find((p) => Array.isArray(p) && p[0] === 'expo-build-properties');
  return Array.isArray(entry) ? (entry[1] as { android?: { usesCleartextTraffic?: boolean } })?.android?.usesCleartextTraffic : undefined;
}

export default ({ config }: ConfigContext): ExpoConfig => buildConfig(config as ExpoConfig, process.env);
