import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { FilterSheet } from '@/components/FilterSheet';
import { VenueGridSkeleton } from '@/components/Skeleton';
import { VenueProductCard } from '@/components/VenueProductCard';
import { api } from '@/lib/api';
import { peekRecommend, recommendCacheKey } from '@/lib/recommend-cache';
import { INTENTS, intentLabel } from '@/lib/constants';
import { emptyDiscover, friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { haptic } from '@/lib/haptics';
import { INTENT_PHOTOS } from '@/lib/photos';
import { radiusLabel } from '@/lib/radius';
import { activeFilterCount, sortCards, SORTS, type SortKey } from '@/lib/sort';
import { colors, fonts } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

const PAGE = 20;

export default function Discover() {
  const insets = useSafeAreaInsets();
  const filters = useFilters();
  const [items, setItems] = useState<VenueCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sheet, setSheet] = useState(false);
  const [sort, setSort] = useState<SortKey>('smart');
  const [visibleIds, setVisibleIds] = useState<Set<string>>(new Set());
  // Filtre hızla değişirse eski isteğin cevabı yenisinin üstüne yazmasın.
  const seq = useRef(0);
  const { lat, lng, intent, openNow, vibes, radiusMeters, minRating } = filters;
  const onViewableItemsChanged = useRef(({ viewableItems }: { viewableItems: Array<{ item?: VenueCard }> }) => {
    setVisibleIds(new Set(viewableItems.map((row) => row.item?.placeId).filter((id): id is string => Boolean(id))));
  }).current;

  const fetchPage = useCallback(
    async (skip: number) => {
      const res = await api.recommend(lat, lng, intent, openNow, vibes, radiusMeters, skip, PAGE);
      return {
        cards: res.items.filter((c) => !minRating || (c.nereyeRating ?? 0) >= minRating),
        hasMore: res.hasMore,
        fetched: res.items.length,
      };
    },
    [lat, lng, intent, openNow, vibes, radiusMeters, minRating],
  );

  const offset = useRef(0);

  const load = useCallback(async () => {
    const n = ++seq.current;
    setError(null);
    const cached = peekRecommend(
      recommendCacheKey({
        lat,
        lng,
        intent,
        radiusMeters,
        openNow,
        vibes,
        skip: 0,
        take: PAGE,
      }),
    );
    if (cached) {
      const cards = cached.items.filter((c) => !minRating || (c.nereyeRating ?? 0) >= minRating);
      offset.current = cached.items.length;
      setItems(cards);
      setHasMore(cached.hasMore);
      setLoading(false);
    } else {
      setLoading(true);
    }
    try {
      const page = await fetchPage(0);
      if (n !== seq.current) {
        return;
      }
      offset.current = page.fetched;
      setItems(page.cards);
      setHasMore(page.hasMore);
    } catch (e) {
      if (n !== seq.current) {
        return;
      }
      if (!cached) {
        setError(friendlyError(e));
        setItems([]);
        setHasMore(false);
      }
    } finally {
      if (n === seq.current) {
        setLoading(false);
      }
    }
  }, [fetchPage, lat, lng, intent, radiusMeters, openNow, vibes, minRating]);

  useEffect(() => {
    void load();
  }, [load]);

  async function loadMore() {
    if (loading || loadingMore || !hasMore) {
      return;
    }
    const n = seq.current;
    setLoadingMore(true);
    try {
      const page = await fetchPage(offset.current);
      if (n !== seq.current) {
        return;
      }
      offset.current += page.fetched;
      setItems((prev) => {
        const seen = new Set(prev.map((c) => c.placeId));
        return [...prev, ...page.cards.filter((c) => !seen.has(c.placeId))];
      });
      setHasMore(page.hasMore && page.fetched > 0);
    } catch (e) {
      setError(friendlyError(e));
    } finally {
      setLoadingMore(false);
    }
  }

  const sorted = useMemo(() => sortCards(items, sort), [items, sort]);
  const badge = activeFilterCount(filters);
  const first = loading && items.length === 0;

  const header = (
    <View>
      <View style={styles.head}>
        <View style={{ flex: 1 }}>
          <Text style={styles.h}>{intentLabel(intent)}</Text>
          <Text style={styles.where} numberOfLines={1}>
            {filters.label} · {radiusLabel(radiusMeters)} çevresi
          </Text>
        </View>
        <Pressable
          style={[styles.filter, badge > 0 && styles.filterOn]}
          onPress={() => setSheet(true)}
          accessibilityRole="button"
          accessibilityLabel={badge > 0 ? `Filtre, ${badge} etkin` : 'Filtre'}>
          <Ionicons name="options" size={18} color={badge > 0 ? colors.white : colors.ink} />
          {badge > 0 ? <Text style={styles.badge}>{badge}</Text> : null}
        </Pressable>
      </View>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips}>
        {INTENTS.map((i) => {
          const on = intent === i;
          return (
            <Pressable
              key={i}
              onPress={() => {
                haptic.tap();
                filters.setIntent(i);
              }}
              style={[styles.chip, on && styles.chipOn]}
              accessibilityRole="button"
              accessibilityLabel={intentLabel(i)}
              accessibilityState={{ selected: on }}>
              <Image source={INTENT_PHOTOS[i]} style={styles.chipImg} />
              <Text style={[styles.chipText, on && styles.chipTextOn]}>{intentLabel(i)}</Text>
            </Pressable>
          );
        })}
      </ScrollView>
      <View style={styles.sortRow} accessibilityRole="radiogroup" accessibilityLabel="Sıralama">
        {SORTS.map((s) => {
          const on = sort === s.key;
          return (
            <Pressable
              key={s.key}
              onPress={() => {
                haptic.tap();
                setSort(s.key);
              }}
              style={[styles.sort, on && styles.sortOn]}
              accessibilityRole="radio"
              accessibilityState={{ selected: on }}>
              <Text style={[styles.sortText, on && styles.sortTextOn]}>{s.label}</Text>
            </Pressable>
          );
        })}
      </View>
      {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
      {first ? <VenueGridSkeleton /> : null}
    </View>
  );

  return (
    <View style={styles.bg}>
      <FlatList
        data={first ? [] : sorted}
        keyExtractor={(c) => c.placeId}
        numColumns={2}
        // İlk ekranı doldurmaya yetecek kadar kart çiz; gerisi kaydırdıkça gelir.
        initialNumToRender={6}
        maxToRenderPerBatch={6}
        windowSize={7}
        removeClippedSubviews
        columnWrapperStyle={styles.column}
        contentContainerStyle={[styles.wrap, { paddingTop: insets.top + 8 }]}
        showsVerticalScrollIndicator={false}
        ListHeaderComponent={header}
        renderItem={({ item }) => (
          <View style={styles.cell}>
            <VenueProductCard card={item} intent={intent} loadPhoto={visibleIds.has(item.placeId)} />
          </View>
        )}
        viewabilityConfig={{ itemVisiblePercentThreshold: 30, minimumViewTime: 80 }}
        onViewableItemsChanged={onViewableItemsChanged}
        ListEmptyComponent={
          !loading && !error ? (
            <EmptyState
              icon="search-outline"
              title={emptyDiscover(intent)}
              body={badge > 0 ? 'Etkin filtreler sonucu daraltıyor olabilir.' : undefined}
              action={badge > 0 ? 'Filtreleri temizle' : 'Yarıçapı büyüt'}
              onAction={() => (badge > 0 ? filters.resetFilters() : filters.setRadius(Math.max(3000, radiusMeters * 2)))}
            />
          ) : undefined
        }
        ListFooterComponent={
          loadingMore ? (
            <ActivityIndicator color={colors.coral} style={styles.foot} />
          ) : !hasMore && sorted.length > 6 ? (
            <Text style={styles.end}>Hepsi bu kadar · {sorted.length} mekan</Text>
          ) : undefined
        }
        onEndReached={() => void loadMore()}
        onEndReachedThreshold={0.6}
        refreshControl={
          <RefreshControl refreshing={loading && !first} onRefresh={() => void load()} tintColor={colors.coral} colors={[colors.coral]} />
        }
      />
      <FilterSheet visible={sheet} onClose={() => setSheet(false)} />
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingBottom: 48 },
  head: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 20, marginBottom: 12, gap: 12 },
  h: { fontFamily: fonts.display, fontSize: 32, color: colors.ink },
  where: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, marginTop: 2 },
  filter: {
    minWidth: 44,
    height: 44,
    borderRadius: 22,
    paddingHorizontal: 13,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: 6,
  },
  filterOn: { backgroundColor: colors.coral, borderColor: colors.coral },
  badge: { color: colors.white, fontFamily: fonts.sansBold, fontSize: 13 },
  chips: { paddingHorizontal: 20, gap: 8, paddingBottom: 12 },
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
  chipText: { fontFamily: fonts.sansMed, color: colors.ink, fontSize: 14 },
  chipTextOn: { fontFamily: fonts.sansBold, color: colors.espresso },
  sortRow: { flexDirection: 'row', gap: 6, paddingHorizontal: 20, marginBottom: 16 },
  sort: { minHeight: 36, paddingHorizontal: 12, borderRadius: 18, justifyContent: 'center' },
  sortOn: { backgroundColor: colors.espresso },
  sortText: { color: colors.muted, fontFamily: fonts.sansMed, fontSize: 13 },
  sortTextOn: { color: colors.white, fontFamily: fonts.sansBold },
  column: { paddingHorizontal: 20, gap: 12, marginBottom: 16 },
  cell: { flex: 1, maxWidth: '50%' },
  foot: { marginVertical: 20 },
  end: { textAlign: 'center', color: colors.muted, fontFamily: fonts.sans, fontSize: 13, marginVertical: 20 },
});
