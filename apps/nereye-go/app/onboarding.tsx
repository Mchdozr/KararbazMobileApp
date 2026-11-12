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
import { colors, fonts } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
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
      <ScrollView
        contentContainerStyle={[styles.wrap, { paddingTop: insets.top + 16 }]}
        showsVerticalScrollIndicator={false}>
        <View style={styles.head}>
          <BrandLogo size={40} />
          <Pressable onPress={() => void finish('skip')} hitSlop={12} accessibilityRole="button" accessibilityLabel="Atla">
            <Text style={styles.skip}>Atla</Text>
          </Pressable>
        </View>
        <Text style={styles.h}>Bugün canın{'\n'}ne çekiyor?</Text>
        <Text style={styles.sub}>Bir ya da birkaç niyet seç; önerileri ona göre dizelim.</Text>
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
                accessibilityLabel={intentLabel(i)}>
                <Image source={INTENT_PHOTOS[i]} style={StyleSheet.absoluteFill} contentFit="cover" />
                <LinearGradient colors={['rgba(4,56,43,0.05)', 'rgba(4,56,43,0.78)']} style={StyleSheet.absoluteFill} />
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
  wrap: { paddingHorizontal: 20, paddingBottom: 24 },
  head: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  skip: { color: colors.muted, fontFamily: fonts.sansBold, fontSize: 15, padding: 8 },
  h: { fontFamily: fonts.display, fontSize: 34, lineHeight: 40, color: colors.ink, marginTop: 20 },
  sub: { color: colors.muted, marginBottom: 20, fontSize: 16, lineHeight: 22, marginTop: 8, fontFamily: fonts.sans },
  grid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', rowGap: 12 },
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
  tileOn: { borderColor: colors.coral },
  check: {
    position: 'absolute',
    top: 8,
    right: 8,
    width: 26,
    height: 26,
    borderRadius: 13,
    backgroundColor: colors.coral,
    alignItems: 'center',
    justifyContent: 'center',
  },
  tileText: { color: colors.white, fontFamily: fonts.display, fontSize: 20 },
  foot: {
    paddingHorizontal: 20,
    paddingTop: 12,
    gap: 10,
    backgroundColor: colors.bg,
    borderTopWidth: 1,
    borderTopColor: colors.line,
  },
  footRow: { flexDirection: 'row', gap: 10 },
  half: { flex: 1 },
});
