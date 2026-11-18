import { AppErrorBoundary } from '@/components/AppErrorBoundary';
import { BackButton } from '@/components/BackButton';
import { gradientHeader } from '@/components/ScreenHeader';
import { SessionProvider } from '@/lib/session';
import { colors, fonts } from '@/lib/theme';
import { ToastHost } from '@/lib/toast';
import {
  DMSans_400Regular,
  DMSans_500Medium,
  DMSans_700Bold,
} from '@expo-google-fonts/dm-sans';
import { Fraunces_700Bold } from '@expo-google-fonts/fraunces';
import { useFonts } from 'expo-font';
import { DefaultTheme, Stack, ThemeProvider } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { ActivityIndicator, Platform, View } from 'react-native';

const navTheme = {
  ...DefaultTheme,
  colors: { ...DefaultTheme.colors, primary: colors.coral, background: colors.bg, card: colors.surface },
};

// Web: sayfa dili Türkçe olsun. Büyük harf dönüşümü "i"yi "İ" yapar, ekran okuyucu doğru telaffuz eder.
if (Platform.OS === 'web' && typeof document !== 'undefined') {
  document.documentElement.lang = 'tr';
}

export default function RootLayout() {
  const [loaded] = useFonts({
    Fraunces_700Bold,
    DMSans_400Regular,
    DMSans_500Medium,
    DMSans_700Bold,
  });

  if (!loaded) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg }}>
        <ActivityIndicator color={colors.coral} />
      </View>
    );
  }

  return (
    <AppErrorBoundary>
      <SessionProvider>
        <ThemeProvider value={navTheme}>
          <StatusBar style="dark" />
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
                title: 'Yorumun',
                presentation: 'modal',
                header: gradientHeader('Deneyimini paylaş'),
              }}
            />
            <Stack.Screen
              name="owner/[id]"
              options={{
                title: 'İşletme',
                header: gradientHeader('Mekanını yönet'),
              }}
            />
            {[
              ['favorites', 'Kaydedilenler', 'Kalbe dokunduğun mekanlar'],
              ['my-reviews', 'Yorumlarım', 'Yazdıkların ve puanların'],
              ['account', 'Hesap', 'Profil, şifre ve gizlilik'],
              ['legal', 'Gizlilik ve KVKK', 'Verilerin nasıl işlenir'],
            ].map(([name, title, subtitle]) => (
              <Stack.Screen key={name} name={name} options={{ title, header: gradientHeader(subtitle) }} />
            ))}
            <Stack.Screen name="+not-found" options={{ title: 'Sayfa bulunamadı', header: gradientHeader() }} />
          </Stack>
          <ToastHost />
        </ThemeProvider>
      </SessionProvider>
    </AppErrorBoundary>
  );
}
