// Dil, diğer modüllerden önce seçilir (lib/locale.ts).
import '@/lib/locale';
import { t } from '@/lib/i18n';
import { AppErrorBoundary } from '@/components/AppErrorBoundary';
import { BackButton } from '@/components/BackButton';
import { gradientHeader } from '@/components/ScreenHeader';
import { hydrateCovers } from '@/lib/covers';
import { installCrashReporting, setCrashScreen } from '@/lib/crash';
import { installPushHandlers } from '@/lib/push';
import { hydrateAnalytics, track } from '@/lib/analytics';
import { SessionProvider, useSession } from '@/lib/session';
import { colors, fonts, scheme, statusBarOnBg } from '@/lib/theme';
import { ToastHost } from '@/lib/toast';
import {
  DMSans_400Regular,
  DMSans_500Medium,
  DMSans_700Bold,
} from '@expo-google-fonts/dm-sans';
import { Fraunces_700Bold } from '@expo-google-fonts/fraunces';
import { useFonts } from 'expo-font';
import { DarkTheme, DefaultTheme, Stack, ThemeProvider, useRouter, useSegments } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { Platform } from 'react-native';

const baseNav = scheme === 'dark' ? DarkTheme : DefaultTheme;
const navTheme = {
  ...baseNav,
  colors: { ...baseNav.colors, primary: colors.coral, background: colors.bg, card: colors.surface, text: colors.ink, border: colors.line },
};

// Yerel açılış ekranı fontlar ve oturum hazır olana dek kalır: arada boş ekran ya da yükleniyor çarkı görünmez.
void SplashScreen.preventAutoHideAsync().catch(() => {});
SplashScreen.setOptions({ duration: 250, fade: true });

/** Oturum okunduğunda (ve en geç birkaç saniyede) açılış ekranını kaldırır. Derin bağlantıyla açılışta da çalışır. */
function SplashGate() {
  const { ready } = useSession();
  useEffect(() => {
    if (ready) {
      SplashScreen.hide();
    }
  }, [ready]);
  return null;
}

// Önceki oturumda görülen kapak görselleri: ilk kartlar ağ beklemeden gerçek fotoğrafla açılsın.
void hydrateCovers();
// Hata ve çökme raporları (kişisel veri ayıklanmış, hesaba bağlanmaz).
installCrashReporting();
// Kullanım istatistiği yalnızca açık rızayla (lib/analytics.ts); rıza yoksa track hiçbir şey yapmaz.
void hydrateAnalytics().then(() => track('app_open'));

/** Bildirime dokununca ilgili mekan/oylama/liste ekranı açılır. */
function PushRouter() {
  const router = useRouter();
  useEffect(() => installPushHandlers(router), [router]);
  return null;
}

/** Hata raporuna hangi ekranda olduğunu ekler (yol kalıbı; mekan kimliği gibi değerler değil). */
function CrashScreenTracker() {
  const segments = useSegments();
  useEffect(() => {
    setCrashScreen('/' + segments.join('/'));
  }, [segments]);
  return null;
}


export default function RootLayout() {
  const [loaded] = useFonts({
    Fraunces_700Bold,
    DMSans_400Regular,
    DMSans_500Medium,
    DMSans_700Bold,
  });

  // Güvenlik ağı: bir şey takılırsa açılış ekranı sonsuza dek kalmasın.
  useEffect(() => {
    const t = setTimeout(() => SplashScreen.hide(), 6000);
    return () => clearTimeout(t);
  }, []);

  if (!loaded) {
    return null; // açılış ekranı hâlâ görünür
  }

  return (
    <AppErrorBoundary>
      <SessionProvider>
        <SplashGate />
        <CrashScreenTracker />
        <PushRouter />
        <ThemeProvider value={navTheme}>
          <StatusBar style={statusBarOnBg} />
          <Stack
            screenOptions={{
              headerTintColor: colors.ink,
              headerStyle: { backgroundColor: colors.bg },
              headerShadowVisible: false,
              headerBackTitle: '',
              headerTitleStyle: { fontFamily: fonts.display, fontSize: 19, color: colors.ink },
              headerTitleAlign: 'center',
              contentStyle: { backgroundColor: colors.bg },
            }}>
            <Stack.Screen name="index" options={{ headerShown: false }} />
            <Stack.Screen name="auth" options={{ headerShown: false, presentation: 'modal' }} />
            <Stack.Screen name="onboarding" options={{ headerShown: false }} />
            <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
            <Stack.Screen name="places" options={{ title: '', headerShown: false }} />
            <Stack.Screen
              name="venue/[id]"
              options={{
                title: '',
                // Fotoğraf ekranın tepesine kadar uzanır; düğmeler fotoğrafın üstünde cam görünümlü durur.
                headerTransparent: true,
                headerStyle: { backgroundColor: 'transparent' },
                headerBackVisible: false,
                headerLeft: () => <BackButton light />,
              }}
            />
            <Stack.Screen
              name="review/[id]"
              options={{
                title: t('Yorumun'),
                presentation: 'modal',
                header: gradientHeader(t('Deneyimini paylaş')),
              }}
            />
            <Stack.Screen name="collections/index" options={{ title: 'Listelerim', header: gradientHeader(t('Adlandırılmış listelerin')) }} />
            <Stack.Screen name="collections/[id]" options={{ title: 'Liste', header: gradientHeader(t('Mekanlar ve paylaşım')) }} />
            <Stack.Screen name="list/[code]" options={{ title: t('Paylaşılan liste'), header: gradientHeader(t('Nereye ile hazırlandı')) }} />
            <Stack.Screen
              name="poll/[code]"
              options={{
                title: t('Birlikte karar'),
                header: gradientHeader(t('Arkadaşlarınla oyla')),
              }}
            />
            <Stack.Screen
              name="owner/[id]"
              options={{
                title: t('İşletme'),
                header: gradientHeader(t('Mekanını yönet')),
              }}
            />
            {[
              ['favorites', 'Kaydedilenler', t('Kalbe dokunduğun mekanlar')],
              ['my-reviews', t('Yorumlarım'), t('Yazdıkların ve puanların')],
              ['account', 'Hesap', t('Profil, şifre ve gizlilik')],
              ['legal', t('Gizlilik ve KVKK'), t('Verilerin nasıl işlenir')],
            ].map(([name, title, subtitle]) => (
              <Stack.Screen key={name} name={name} options={{ title, header: gradientHeader(subtitle) }} />
            ))}
            <Stack.Screen name="+not-found" options={{ title: t('Sayfa bulunamadı'), header: gradientHeader() }} />
          </Stack>
          <ToastHost />
        </ThemeProvider>
      </SessionProvider>
    </AppErrorBoundary>
  );
}
