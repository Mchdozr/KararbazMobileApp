import { BackButton } from '@/components/BackButton';
import { BrandLogo } from '@/components/BrandLogo';
import { INTENTS, intentLabel } from '@/lib/constants';
import { detectLocation } from '@/lib/geo';
import { INTENT_PHOTOS } from '@/lib/photos';
import { api } from '@/lib/api';
import { useFilters } from '@/lib/filters';
import { markOnboarded } from '@/lib/onboard';
import { useSession } from '@/lib/session';
import { colors, fonts } from '@/lib/theme';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useState } from 'react';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { KADIKOY } from '@/lib/constants';

export default function Onboarding() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const filters = useFilters();
  const { session } = useSession();
  const [picked, setPicked] = useState<string[]>([filters.intent]);
  const [note, setNote] = useState<string | null>(null);

  function toggle(intent: string) {
    setPicked((curr) => (curr.includes(intent) ? curr.filter((x) => x !== intent) : [...curr, intent]));
  }

  async function finish(kind: 'kadikoy' | 'here' | 'skip') {
    const first = picked[0] ?? 'Kahve';
    filters.setIntent(first);
    if (session) {
      void api.savePreferences({
        defaultRadiusMeters: filters.radiusMeters,
        intents: picked.length ? picked : [first],
        vibes: filters.vibes,
      });
    }
    if (kind === 'kadikoy') {
      filters.setLoc(KADIKOY.lat, KADIKOY.lng, 'Kadıköy', true);
    } else if (kind === 'here') {
      const fix = await detectLocation();
      if (fix) {
        filters.setLoc(fix.lat, fix.lng, fix.label, false);
        if (session) {
          void api.consent('location', true);
        }
      } else {
        filters.setLoc(KADIKOY.lat, KADIKOY.lng, 'Kadıköy', true);
        setNote('Konum yok; Kadıköy açılıyor.');
      }
    }
    await markOnboarded();
    router.replace('/(tabs)');
  }

  return (
    <ScrollView style={styles.bg} contentContainerStyle={[styles.wrap, { paddingTop: insets.top + 12 }]}>
      {router.canGoBack() ? (
        <View style={styles.backRow}>
          <BackButton />
        </View>
      ) : null}
      <BrandLogo size={40} />
      <Text style={styles.h}>Bugün ne istiyorsun?</Text>
      <Text style={styles.sub}>Bir veya birkaç niyet seç. Konuma göre bakalım.</Text>
      <View style={styles.grid}>
        {INTENTS.map((i) => (
          <Pressable
            key={i}
            onPress={() => toggle(i)}
            style={[styles.tile, picked.includes(i) && styles.tileOn]}
            accessibilityRole="button"
            accessibilityState={{ selected: picked.includes(i) }}
            accessibilityLabel={intentLabel(i)}>
            <Image source={INTENT_PHOTOS[i]} style={StyleSheet.absoluteFill} contentFit="cover" />
            <LinearGradient colors={['transparent', 'rgba(6,78,59,)0.72)']} style={StyleSheet.absoluteFill} />
            <Text style={styles.tileText}>{intentLabel(i)}</Text>
          </Pressable>
        ))}
      </View>
      {note ? <Text style={styles.note}>{note}</Text> : null}
      <Pressable style={styles.btn} onPress={() => void finish('kadikoy')} accessibilityRole="button">
        <Text style={styles.btnText}>Kadıköy’de öner</Text>
      </Pressable>
      <Pressable style={styles.ghost} onPress={() => void finish('here')} accessibilityRole="button">
        <Text style={styles.ghostText}>Konumumu kullan</Text>
      </Pressable>
      <Pressable style={styles.ghost} onPress={() => router.push('/places')} accessibilityRole="button">
        <Text style={styles.ghostText}>Mahalle seç / kayıtlı yerler</Text>
      </Pressable>
      <Pressable style={styles.ghost} onPress={() => void finish('skip')} accessibilityRole="button" accessibilityLabel="Atla">
        <Text style={styles.ghostText}>Atla</Text>
      </Pressable>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingHorizontal: 20, paddingBottom: 40 },
  backRow: { marginBottom: 8 },
  h: { fontFamily: fonts.display, fontSize: 32, color: colors.ink, marginTop: 8 },
  sub: { color: colors.muted, marginBottom: 18, fontSize: 16, marginTop: 6 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  tile: {
    width: '48%',
    height: 128,
    borderRadius: 20,
    overflow: 'hidden',
    justifyContent: 'flex-end',
    padding: 12,
    backgroundColor: colors.line,
  },
  tileOn: { borderWidth: 2, borderColor: colors.coral },
  tileText: { color: colors.white, fontFamily: fonts.display, fontSize: 20, zIndex: 1 },
  note: { color: colors.muted, marginTop: 12 },
  btn: { marginTop: 22, backgroundColor: colors.coral, padding: 16, minHeight: 44, borderRadius: 16, alignItems: 'center' },
  btnText: { color: colors.white, fontWeight: '800', fontSize: 16 },
  ghost: { marginTop: 12, alignItems: 'center', padding: 12, minHeight: 44 },
  ghostText: { color: colors.coral, fontWeight: '700' },
});
