import { BrandLogo } from '@/components/BrandLogo';
import { Button } from '@/components/Button';
import { api } from '@/lib/api';
import { INTENTS, KADIKOY, intentLabel } from '@/lib/constants';
import { useFilters } from '@/lib/filters';
import { detectLocation } from '@/lib/geo';
import { haptic } from '@/lib/haptics';
import { markOnboarded } from '@/lib/onboard';
import { INTENT_PHOTOS } from '@/lib/photos';
import { useSession } from '@/lib/session';
import { colors, fonts, gradients, shadows } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { StatusBar } from 'expo-status-bar';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export default function Onboarding() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const filters = useFilters();
  const { session } = useSession();
  const [picked, setPicked] = useState<string[]>([filters.intent]);
  const [busy, setBusy] = useState<'here' | 'kadikoy' | null>(null);

  function toggle(intent: string) {
    haptic.tap();
    setPicked((curr) => (curr.includes(intent) ? curr.filter((x) => x !== intent) : [...curr, intent]));
  }

  async function finish(kind: 'kadikoy' | 'here' | 'skip' | 'pick') {
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
      setBusy('here');
      try {
        const fix = await detectLocation();
        if (fix) {
          filters.setLoc(fix.lat, fix.lng, fix.label, false);
          if (session) {
            void api.consent('location', true);
          }
        } else {
          filters.setLoc(KADIKOY.lat, KADIKOY.lng, 'Kadıköy', true);
          toast.info('Konum izni yok; Kadıköy ile başlıyoruz. Dilediğinde değiştirebilirsin.');
        }
      } catch {
        filters.setLoc(KADIKOY.lat, KADIKOY.lng, 'Kadıköy', true);
        toast.info('Konum alınamadı; Kadıköy ile başlıyoruz.');
      } finally {
        setBusy(null);
      }
    }
    await markOnboarded();
    router.replace('/(tabs)');
    if (kind === 'pick') {
      router.push('/places');
    }
  }

  return (
    <View style={styles.bg}>
      <ScrollView contentContainerStyle={styles.wrap} showsVerticalScrollIndicator={false}>
        <StatusBar style="light" />
        <LinearGradient colors={gradients.hero} style={[styles.hero, { paddingTop: insets.top + 14 }]}>
          <View style={styles.head}>
            <View style={styles.logoTile}>
              <BrandLogo size={34} />
            </View>
            <Pressable
              style={styles.skipBtn}
              onPress={() => void finish('skip')}
              hitSlop={8}
              accessibilityRole="button"
              accessibilityLabel="Atla">
              <Text style={styles.skip}>Atla</Text>
            </Pressable>
          </View>
          <Text style={styles.h}>Bugün canın{'\n'}ne çekiyor?</Text>
          <Text style={styles.sub}>Bir ya da birkaç niyet seç; önerileri ona göre dizelim.</Text>
        </LinearGradient>
        <View style={styles.grid}>
          {INTENTS.map((i) => {
            const on = picked.includes(i);
            return (
              <Pressable
                key={i}
                onPress={() => toggle(i)}
                style={[styles.tile, on && styles.tileOn]}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: on }}
                aria-checked={on}
                accessibilityLabel={intentLabel(i)}>
                <Image source={INTENT_PHOTOS[i]} style={StyleSheet.absoluteFill} contentFit="cover" />
                <LinearGradient colors={gradients.photo} style={StyleSheet.absoluteFill} />
                {on ? (
                  <View style={styles.check}>
                    <Ionicons name="checkmark" size={16} color={colors.white} />
                  </View>
                ) : null}
                <Text style={styles.tileText}>{intentLabel(i)}</Text>
              </Pressable>
            );
          })}
        </View>
      </ScrollView>
      <View style={[styles.foot, { paddingBottom: insets.bottom + 12 }]}>
        <Button
          label="Konumumu kullan"
          icon="navigate"
          onPress={() => void finish('here')}
          loading={busy === 'here'}
          accessibilityHint="Yakınındaki mekanları önermek için konum izni ister"
        />
        <View style={styles.footRow}>
          <Button label="Kadıköy ile başla" variant="outline" compact style={styles.half} onPress={() => void finish('kadikoy')} />
          <Button label="Semt seç" variant="outline" compact style={styles.half} onPress={() => void finish('pick')} />
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingBottom: 24 },
  hero: { paddingHorizontal: 20, paddingBottom: 28, borderBottomLeftRadius: 28, borderBottomRightRadius: 28 },
  head: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  logoTile: {
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  skipBtn: {
    minHeight: 44,
    paddingHorizontal: 16,
    borderRadius: 22,
    justifyContent: 'center',
    backgroundColor: colors.glassLight,
    borderWidth: 1,
    borderColor: colors.glassLine,
  },
  skip: { color: colors.white, fontFamily: fonts.sansBold, fontSize: 14 },
  h: { fontFamily: fonts.display, fontSize: 34, lineHeight: 40, color: colors.white, marginTop: 22 },
  sub: { color: colors.onDarkMuted, fontSize: 16, lineHeight: 22, marginTop: 8, fontFamily: fonts.sans },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    rowGap: 12,
    paddingHorizontal: 20,
    marginTop: 20,
  },
  tile: {
    width: '48.2%',
    height: 124,
    borderRadius: 20,
    overflow: 'hidden',
    justifyContent: 'flex-end',
    padding: 12,
    backgroundColor: colors.line,
    borderWidth: 3,
    borderColor: 'transparent',
  },
  tileOn: { borderColor: colors.accent },
  check: {
    position: 'absolute',
    top: 8,
    right: 8,
    width: 26,
    height: 26,
    borderRadius: 13,
    backgroundColor: colors.accent,
    alignItems: 'center',
    justifyContent: 'center',
  },
  tileText: { color: colors.white, fontFamily: fonts.display, fontSize: 20 },
  foot: {
    paddingHorizontal: 20,
    paddingTop: 12,
    gap: 10,
    backgroundColor: colors.surface,
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    ...shadows.sheet,
  },
  footRow: { flexDirection: 'row', gap: 10 },
  half: { flex: 1 },
});
