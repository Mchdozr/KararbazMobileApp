import { Button } from '@/components/Button';
import { RadiusPicker } from '@/components/RadiusPicker';
import { VIBES, vibeLabel } from '@/lib/constants';
import { useFilters } from '@/lib/filters';
import { haptic } from '@/lib/haptics';
import { activeFilterCount } from '@/lib/sort';
import { colors, fonts, radii } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { Modal, Pressable, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

const RATINGS = [0, 3, 4, 4.5];

export function FilterSheet({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  const filters = useFilters();
  const insets = useSafeAreaInsets();
  const count = activeFilterCount(filters);
  return (
    <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={onClose}>
      <View style={styles.sheet}>
        <View style={styles.head}>
          <Text style={styles.title}>Filtre</Text>
          {count > 0 ? (
            <Pressable
              onPress={() => {
                haptic.tap();
                filters.resetFilters();
              }}
              hitSlop={10}
              style={styles.reset}
              accessibilityRole="button"
              accessibilityLabel="Filtreleri temizle">
              <Text style={styles.resetText}>Temizle</Text>
            </Pressable>
          ) : null}
          <Pressable
            style={styles.close}
            onPress={onClose}
            accessibilityRole="button"
            accessibilityLabel="Filtreyi kapat"
            hitSlop={8}>
            <Ionicons name="close" size={20} color={colors.ink} />
          </Pressable>
        </View>
        <ScrollView contentContainerStyle={styles.body}>
          <Text style={styles.label}>Mesafe</Text>
          <RadiusPicker meters={filters.radiusMeters} onChange={filters.setRadius} />
          <View style={styles.toggle}>
            <View style={{ flex: 1 }}>
              <Text style={styles.toggleText}>Şu an açık</Text>
              <Text style={styles.toggleSub}>Kapalı mekanları gizle</Text>
            </View>
            <Switch
              value={filters.openNow}
              onValueChange={(v) => {
                haptic.tap();
                filters.setOpenNow(v);
              }}
              trackColor={{ true: colors.sage, false: colors.line }}
              thumbColor={colors.white}
              accessibilityLabel="Şu an açık"
            />
          </View>
          <Text style={styles.label}>Min. puan</Text>
          <View style={styles.row}>
            {RATINGS.map((n) => {
              const on = filters.minRating === n;
              return (
                <Pressable
                  key={n}
                  style={[styles.chip, on && styles.chipOn]}
                  onPress={() => filters.setMinRating(n)}
                  accessibilityRole="button"
                  accessibilityLabel={n === 0 ? 'Puan yok' : `En az ${n}`}
                  accessibilityState={{ selected: on }} aria-selected={on}>
                  <Text style={[styles.chipText, on && styles.chipTextOn]}>{n === 0 ? 'Hepsi' : `★ ${n}+`}</Text>
                </Pressable>
              );
            })}
          </View>
          <Text style={styles.label}>Atmosfer</Text>
          <View style={styles.row}>
            {VIBES.map((v) => {
              const on = filters.vibes.includes(v);
              return (
                <Pressable
                  key={v}
                  style={[styles.chip, on && styles.chipOn]}
                  onPress={() => filters.toggleVibe(v)}
                  accessibilityRole="button"
                  accessibilityState={{ selected: on }} aria-selected={on}
                  accessibilityLabel={vibeLabel(v)}>
                  <Text style={[styles.chipText, on && styles.chipTextOn]}>{vibeLabel(v)}</Text>
                </Pressable>
              );
            })}
          </View>
        </ScrollView>
        <View style={[styles.foot, { paddingBottom: insets.bottom + 12 }]}>
          <Button label="Sonuçları göster" onPress={onClose} />
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  sheet: { flex: 1, backgroundColor: colors.bg, paddingHorizontal: 20, paddingTop: 16 },
  head: { flexDirection: 'row', alignItems: 'center', marginBottom: 8 },
  title: { fontFamily: fonts.display, fontSize: 28, color: colors.ink, flex: 1 },
  close: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  body: { paddingBottom: 24, gap: 10 },
  label: { marginTop: 12, fontFamily: fonts.sansBold, fontSize: 15, color: colors.ink },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: {
    paddingHorizontal: 12,
    paddingVertical: 10,
    minHeight: 44,
    borderRadius: radii.pill,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    justifyContent: 'center',
  },
  chipOn: { backgroundColor: colors.coral, borderColor: colors.coral },
  chipText: { color: colors.ink, fontWeight: '700' },
  chipTextOn: { color: colors.white },
  toggle: {
    marginTop: 8,
    padding: 14,
    minHeight: 44,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderRadius: radii.md,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
  },
  toggleText: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 15 },
  toggleSub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13, marginTop: 2 },
  reset: { minHeight: 44, justifyContent: 'center', paddingHorizontal: 12 },
  resetText: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 14 },
  foot: { paddingTop: 12, borderTopWidth: 1, borderTopColor: colors.line },
});
