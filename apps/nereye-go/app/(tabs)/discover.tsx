import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { FilterSheet } from '@/components/FilterSheet';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { VenueProductCard } from '@/components/VenueProductCard';
import { INTENTS, intentLabel } from '@/lib/constants';
import { emptyDiscover, friendlyError } from '@/lib/empty';
import { INTENT_PHOTOS } from '@/lib/photos';
import { useFilters } from '@/lib/filters';
import { api } from '@/lib/api';
import { colors, fonts } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { useCallback, useEffect, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export default function Discover() {
  const insets = useSafeAreaInsets();
  const filters = useFilters();
  const [items, setItems] = useState<VenueCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(false);
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
        20,
      );
      setItems(res.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating));
      setHasMore(res.hasMore);
    } catch (e) {
      setError(friendlyError(e));
      setItems([]);
      setHasMore(false);
    } finally {
      setLoading(false);
    }
  }, [filters.lat, filters.lng, filters.intent, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating]);

  async function loadMore() {
    if (loadingMore || !hasMore) {
      return;
    }
    setLoadingMore(true);
    try {
      const res = await api.recommend(
        filters.lat,
        filters.lng,
        filters.intent,
        filters.openNow,
        filters.vibes,
        filters.radiusMeters,
        items.length,
        20,
      );
      const extra = res.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating);
      setItems((prev) => {
        const seen = new Set(prev.map((c) => c.placeId));
        return [...prev, ...extra.filter((c) => !seen.has(c.placeId))];
      });
      setHasMore(res.hasMore);
    } catch (e) {
      setError(friendlyError(e));
    } finally {
      setLoadingMore(false);
    }
  }

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <View style={styles.bg}>
      <ScrollView
        contentContainerStyle={[styles.wrap, { paddingTop: insets.top + 8 }]}
        refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load()} tintColor={colors.coral} />}>
        <View style={styles.head}>
          <Text style={styles.h}>Niyetler</Text>
          <Pressable style={styles.filter} onPress={() => setSheet(true)} accessibilityRole="button" accessibilityLabel="Filtre">
            <Ionicons name="options" size={18} color={colors.ink} />
          </Pressable>
        </View>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips}>
          {INTENTS.map((i) => {
            const on = filters.intent === i;
            return (
              <Pressable
                key={i}
                onPress={() => filters.setIntent(i)}
                style={[styles.chip, on && styles.chipOn]}
                accessibilityRole="button"
                accessibilityState={{ selected: on }}>
                <Image source={INTENT_PHOTOS[i]} style={styles.chipImg} />
                <Text style={[styles.chipText, on && styles.chipTextOn]}>{intentLabel(i)}</Text>
              </Pressable>
            );
          })}
        </ScrollView>
        {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
        {!loading && items.length === 0 && !error ? (
          <EmptyState
            title={emptyDiscover(filters.intent)}
            action="Yarıçapı büyüt"
            onAction={() => filters.setRadius(Math.max(3000, filters.radiusMeters * 2))}
          />
        ) : null}
        <View style={styles.grid}>
          {items.map((card) => (
            <View key={card.placeId} style={styles.cell}>
              <VenueProductCard card={card} intent={filters.intent} />
            </View>
          ))}
        </View>
        {hasMore ? (
          <Pressable style={styles.more} onPress={() => void loadMore()} disabled={loadingMore} accessibilityRole="button">
            <Text style={styles.moreText}>{loadingMore ? 'Yükleniyor…' : 'Daha fazla mekan'}</Text>
          </Pressable>
        ) : null}
      </ScrollView>
      <FilterSheet visible={sheet} onClose={() => setSheet(false)} />
      <SearchingOverlay visible={loading} intent={filters.intent} />
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingBottom: 48 },
  head: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 20, marginBottom: 12 },
  h: { fontFamily: fonts.display, fontSize: 32, color: colors.ink, flex: 1 },
  filter: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  chips: { paddingHorizontal: 20, gap: 8, paddingBottom: 16 },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: colors.surface,
    paddingRight: 14,
    paddingLeft: 4,
    paddingVertical: 4,
    minHeight: 44,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: colors.line,
  },
  chipOn: { borderColor: colors.coral, backgroundColor: colors.coralSoft },
  chipImg: { width: 32, height: 32, borderRadius: 16 },
  chipText: { fontWeight: '700', color: colors.ink },
  chipTextOn: { color: colors.espresso },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12, paddingHorizontal: 20 },
  cell: { width: '47%' },
  more: { marginHorizontal: 20, marginTop: 16, alignItems: 'center', padding: 14, minHeight: 44 },
  moreText: { color: colors.coral, fontWeight: '800' },
});
