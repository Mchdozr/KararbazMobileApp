import { colors, fonts } from '@/lib/theme';
import { getOnboardedAt } from '@/lib/onboard';
import { useSession } from '@/lib/session';
import { Redirect } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';

export default function Index() {
  const { ready, session } = useSession();
  const [onboarded, setOnboarded] = useState<boolean | null>(null);

  useEffect(() => {
    if (!ready) {
      return;
    }
    void getOnboardedAt().then((at) => setOnboarded(Boolean(at)));
  }, [ready]);

  if (!ready || onboarded === null) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg }}>
        <Text style={{ fontSize: 40, fontFamily: fonts.display, color: colors.ink }}>Nereye</Text>
        <ActivityIndicator color={colors.coral} style={{ marginTop: 16 }} />
      </View>
    );
  }

  if (session && !onboarded) {
    return <Redirect href="/onboarding" />;
  }

  return <Redirect href="/(tabs)" />;
}
