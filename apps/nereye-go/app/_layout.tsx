import { AppErrorBoundary } from '@/components/AppErrorBoundary';
import { BackButton } from '@/components/BackButton';
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
import { ActivityIndicator, View } from 'react-native';

const navTheme = {
  ...DefaultTheme,
  colors: { ...DefaultTheme.colors, primary: colors.coral, background: colors.bg, card: colors.surface },
};

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
              headerStyle: { backgroundColor: colors.surface },
              headerShadowVisible: false,
              headerBackTitle: '',
              headerTitleStyle: { fontFamily: fonts.sansBold, fontSize: 17, color: colors.ink },
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
                headerBackVisible: false,
                headerLeft: () => <BackButton />,
              }}
            />
            <Stack.Screen
              name="review/[id]"
              options={{
                title: 'Yorumun',
                presentation: 'modal',
                headerBackVisible: false,
                headerLeft: () => <BackButton />,
              }}
            />
            <Stack.Screen
              name="owner/[id]"
              options={{
                title: 'İşletme',
                headerBackVisible: false,
                headerLeft: () => <BackButton />,
              }}
            />
            {[
              ['favorites', 'Kaydedilenler'],
              ['my-reviews', 'Yorumlarım'],
              ['account', 'Hesap'],
              ['legal', 'Gizlilik ve KVKK'],
            ].map(([name, title]) => (
              <Stack.Screen
                key={name}
                name={name}
                options={{ title, headerBackVisible: false, headerLeft: () => <BackButton /> }}
              />
            ))}
          </Stack>
          <ToastHost />
        </ThemeProvider>
      </SessionProvider>
    </AppErrorBoundary>
  );
}
