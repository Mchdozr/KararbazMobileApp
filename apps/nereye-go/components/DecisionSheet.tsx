import { Button } from '@/components/Button';
import { VenueImage } from '@/components/VenueImage';
import { api } from '@/lib/api';
import { pickForMe, type Decision } from '@/lib/decide';
import { haptic } from '@/lib/haptics';
import { openDirections } from '@/lib/maps';
import { minutesOfDay } from '@/lib/open-status';
import { colors, fonts, radii, shadows } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { metaLine } from '@/lib/venue-format';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import Animated, { useAnimatedStyle, useSharedValue, withSpring, withTiming } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/**
 * "Benim için seç": listedeki mekanlardan şu an için en uygununu gerekçeleriyle gösterir.
 * "Başka öner" sıradakine geçer; hepsi görülünce baştan başlar.
 */
export function DecisionSheet({
  visible,
  cards,
  intentLabel,
  origin,
  onClose,
}: {
  visible: boolean;
  cards: readonly VenueCard[];
  intentLabel: string;
  origin: { lat: number; lng: number };
  onClose: () => void;
}) {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [seen, setSeen] = useState<string[]>([]);
  const [decision, setDecision] = useState<Decision | null>(null);
  const enter = useSharedValue(0);
  const style = useAnimatedStyle(() => ({
    opacity: enter.value,
    transform: [{ translateY: (1 - enter.value) * 24 }, { scale: 0.96 + 0.04 * enter.value }],
  }));

  function show(next: Decision | null) {
    setDecision(next);
    if (next) {
      setSeen((prev) => (prev.includes(next.card.placeId) ? [next.card.placeId] : [...prev, next.card.placeId]));
      api.prefetchVenue(next.card.placeId);
      enter.value = 0;
      enter.value = withSpring(1, { damping: 16, stiffness: 180 });
    }
  }

  // Her açılışta en iyi seçimden başlanır.
  useEffect(() => {
    if (visible) {
      setSeen([]);
      show(pickForMe(cards, [], minutesOfDay()));
      haptic.success();
    } else {
      enter.value = withTiming(0, { duration: 120 });
    }
  }, [visible]);

  function another() {
    haptic.tap();
    show(pickForMe(cards, seen, minutesOfDay()));
  }

  const card = decision?.card;
  const meta = card ? metaLine(card.category, card.priceLevel) : '';

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose} statusBarTranslucent>
      <Pressable style={styles.backdrop} onPress={onClose} accessibilityRole="button" accessibilityLabel="Seçimi kapat" />
      <View style={[styles.sheet, { paddingBottom: insets.bottom + 16 }]} accessibilityViewIsModal>
        <View style={styles.grip} />
        <View style={styles.head}>
          <View style={styles.headIcon}>
            <Ionicons name="sparkles" size={16} color={colors.accent} />
          </View>
          <View style={{ flex: 1 }}>
            <Text style={styles.kicker}>{decision ? `${intentLabel} · ${decision.position}/${decision.total}` : intentLabel}</Text>
            <Text style={styles.title}>Senin için seçtim</Text>
          </View>
          <Pressable onPress={onClose} style={styles.close} hitSlop={8} accessibilityRole="button" accessibilityLabel="Kapat">
            <Ionicons name="close" size={20} color={colors.ink} />
          </Pressable>
        </View>

        {card && decision ? (
          <Animated.View style={style}>
            <View style={styles.photo}>
              <VenueImage
                placeId={card.placeId}
                photoUrl={card.photoUrl}
                photoReference={card.photoReference}
                name={card.name}
                style={StyleSheet.absoluteFill}
                radius={radii.md}
                active
              />
            </View>
            <Text style={styles.name} numberOfLines={2}>
              {card.name}
            </Text>
            {meta ? <Text style={styles.meta}>{meta}</Text> : null}
            <View style={styles.reasons}>
              {decision.reasons.map((r) => (
                <View key={r.text} style={styles.reason}>
                  <Ionicons name={r.icon} size={14} color={r.icon === 'star' ? colors.gold : colors.coral} />
                  <Text style={styles.reasonText}>{r.text}</Text>
                </View>
              ))}
            </View>
            {card.note ? (
              <Text style={styles.note} numberOfLines={2}>
                {card.note}
              </Text>
            ) : null}
            <View style={styles.actions}>
              <Button
                label="Yol tarifi"
                icon="navigate"
                onPress={() => {
                  haptic.press();
                  void openDirections(card.latitude, card.longitude, origin.lat, origin.lng);
                }}
                style={{ flex: 1 }}
                accessibilityLabel={`${card.name} için yol tarifi`}
              />
              <Button
                label="Detaya bak"
                variant="secondary"
                onPress={() => {
                  onClose();
                  router.push({ pathname: '/venue/[id]', params: { id: card.placeId, name: card.name, photo: card.photoUrl ?? '' } });
                }}
                style={{ flex: 1 }}
                accessibilityLabel={`${card.name} detayını aç`}
              />
            </View>
            {decision.total > 1 ? (
              <Pressable onPress={another} style={styles.another} accessibilityRole="button" accessibilityLabel="Başka bir mekan öner">
                <Ionicons name="shuffle" size={18} color={colors.coral} />
                <Text style={styles.anotherText}>Başka öner</Text>
              </Pressable>
            ) : null}
          </Animated.View>
        ) : (
          <Text style={styles.empty}>Seçebileceğim bir mekan yok. Yarıçapı büyütmeyi ya da filtreleri gevşetmeyi dene.</Text>
        )}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(6,29,22,0.55)' },
  sheet: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: colors.bg,
    borderTopLeftRadius: radii.lg,
    borderTopRightRadius: radii.lg,
    paddingHorizontal: 20,
    paddingTop: 10,
    ...shadows.sheet,
  },
  grip: { alignSelf: 'center', width: 44, height: 5, borderRadius: 3, backgroundColor: colors.line, marginBottom: 12 },
  head: { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: 14 },
  headIcon: { width: 36, height: 36, borderRadius: 18, backgroundColor: colors.accentSoft, alignItems: 'center', justifyContent: 'center' },
  kicker: { color: colors.muted, fontFamily: fonts.sansBold, fontSize: 12 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 22 },
  close: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  photo: { height: 190, borderRadius: radii.md, overflow: 'hidden', backgroundColor: colors.line },
  name: { marginTop: 14, color: colors.ink, fontFamily: fonts.display, fontSize: 24 },
  meta: { marginTop: 2, color: colors.coral, fontFamily: fonts.sansBold, fontSize: 13 },
  reasons: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 12 },
  reason: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 10,
    paddingVertical: 7,
    borderRadius: radii.pill,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
  },
  reasonText: { color: colors.ink, fontFamily: fonts.sansMed, fontSize: 13 },
  note: { marginTop: 10, color: colors.muted, fontFamily: fonts.sans, fontSize: 13, lineHeight: 19 },
  actions: { flexDirection: 'row', gap: 10, marginTop: 16 },
  another: {
    marginTop: 10,
    minHeight: 44,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  anotherText: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 15 },
  empty: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20, paddingVertical: 24 },
});
