import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { FilterSheet } from '@/components/FilterSheet';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { VenueProductCard } from '@/components/VenueProductCard';
import { api } from '@/lib/api';
import { emptyMap, friendlyError } from '@/lib/empty';
import { detectLocation } from '@/lib/geo';
import { isLocPinned, useFilters } from '@/lib/filters';
import { colors } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

export default function MapWebScreen() {
  const filters = useFilters();
  const [items, setItems] = useState<VenueCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sheet, setSheet] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.recommend(
        filters.lat,
        filters.lng,
        filters.intent,
        filters.openNow,
        filters.vibes,
        filters.radiusMeters,
        0,
        40,
      );
      setItems(res.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating));
    } catch (e) {
      setError(friendlyError(e));
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [filters.lat, filters.lng, filters.intent, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating]);

  useEffect(() => {
    void load();
  }, [load]);

  useFocusEffect(
    useCallback(() => {
      if (isLocPinned()) {
        return;
      }
      let alive = true;
      void detectLocation().then((fix) => {
        if (alive && fix && !isLocPinned()) {
          filters.setLoc(fix.lat, fix.lng, fix.label, false);
        }
      });
      return () => {
        alive = false;
      };
    }, []),
  );

  return (
    <View style={styles.bg}>
      <View style={styles.top}>
        <Pressable style={styles.chip} onPress={() => setSheet(true)} accessibilityRole="button" accessibilityLabel={`Konum ${filters.label}`}>
          <Ionicons name="location" size={14} color={colors.coral} />
          <Text style={styles.chipText}>{filters.label}</Text>
        </Pressable>
        <Pressable
          style={styles.here}
          disabled={busy}
          onPress={() => {
            setBusy(true);
            void detectLocation().then((fix) => {
              if (fix) {
                filters.setLoc(fix.lat, fix.lng, fix.label, false);
              }
              setBusy(false);
            });
          }}
          accessibilityRole="button"
          accessibilityLabel="Konumumu kullan">
          <Text style={styles.hereText}>{busy ? 'Alınıyor…' : 'GPS'}</Text>
        </Pressable>
      </View>
      <Text style={styles.note}>Harita telefon uygulamasında açılır. Burada yakın mekanlar listelenir.</Text>
      {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
      <ScrollView contentContainerStyle={styles.list}>
        {!loading && items.length === 0 && !error ? <EmptyState title={emptyMap(filters.intent)} /> : null}
        {items.map((card) => (
          <VenueProductCard key={card.placeId} card={card} intent={filters.intent} />
        ))}
      </ScrollView>
      <FilterSheet visible={sheet} onClose={() => setSheet(false)} />
      <SearchingOverlay visible={loading} intent={filters.intent} />
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  top: { flexDirection: 'row', gap: 8, padding: 16 },
  chip: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.surface,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: colors.line,
    paddingHorizontal: 12,
    minHeight: 44,
  },
  chipText: { color: colors.ink, fontWeight: '700' },
  here: { backgroundColor: colors.coral, borderRadius: 999, paddingHorizontal: 14, justifyContent: 'center' },
  hereText: { color: colors.white, fontWeight: '800' },
  note: { color: colors.muted, paddingHorizontal: 16, marginBottom: 8 },
  list: { padding: 16, gap: 16, paddingBottom: 48 },
});
