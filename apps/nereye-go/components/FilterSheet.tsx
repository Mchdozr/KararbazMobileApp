import { RadiusPicker } from '@/components/RadiusPicker';
import { VIBES, vibeLabel } from '@/lib/constants';
import { useFilters } from '@/lib/filters';
import { colors, fonts, radii } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

const RATINGS = [0, 3, 4, 4.5];

export function FilterSheet({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  const filters = useFilters();
  return (
    <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={onClose}>
      <View style={styles.sheet}>
        <View style={styles.head}>
          <Text style={styles.title}>Filtre</Text>
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
          <Pressable
            style={[styles.toggle, filters.openNow && styles.toggleOn]}
            onPress={() => filters.setOpenNow(!filters.openNow)}
            accessibilityRole="switch"
            accessibilityState={{ checked: filters.openNow }}
            accessibilityLabel="Şu an açık">
            <Text style={[styles.toggleText, filters.openNow && styles.chipTextOn]}>Şu an açık</Text>
          </Pressable>
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
                  accessibilityState={{ selected: on }}>
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
                  accessibilityState={{ selected: on }}
                  accessibilityLabel={vibeLabel(v)}>
                  <Text style={[styles.chipText, on && styles.chipTextOn]}>{vibeLabel(v)}</Text>
                </Pressable>
              );
            })}
          </View>
        </ScrollView>
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
  body: { paddingBottom: 40, gap: 10 },
  label: { marginTop: 12, fontWeight: '800', color: colors.ink },
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
    borderRadius: radii.md,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
  },
  toggleOn: { backgroundColor: colors.espresso, borderColor: colors.espresso },
  toggleText: { color: colors.ink, fontWeight: '800' },
});
