import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { CategoryGrid } from '@/components/home/CategoryGrid';
import { HomeHeader } from '@/components/home/HomeHeader';
import { HorizontalVenueRow } from '@/components/home/HorizontalVenueRow';
import { PromoBanner } from '@/components/home/PromoBanner';
import { VenueProductCard } from '@/components/VenueProductCard';
import { isEvening } from '@/lib/copy';
import { intentLabel } from '@/lib/constants';
import { emptyNearby, friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { api } from '@/lib/api';
import { colors, fonts } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

export default function Home() {
  const filters = useFilters();
  const router = useRouter();
  const [items, setItems] = useState<VenueCard[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [showAll, setShowAll] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    setShowAll(false);
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
      const next = res.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating);
      setItems(next);
      setHasMore(res.hasMore);
    } catch (e) {
      setError(friendlyError(e));
      setItems([]);
      setHasMore(false);
    } finally {
      setLoading(false);
    }
  }, [filters.lat, filters.lng, filters.intent, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating]);

  useEffect(() => {
    void load();
  }, [load]);

  function goDiscover(intent: string) {
    filters.setIntent(intent);
    router.push('/(tabs)/discover');
  }

  async function expand() {
    setShowAll(true);
    if (!hasMore || loadingMore) {
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

  const organic = useMemo(() => items.filter((c) => c.kind !== 'Sponsored'), [items]);
  const sponsored = useMemo(() => items.filter((c) => c.kind === 'Sponsored'), [items]);
  const featured = organic[0];
  const special = organic.slice(0, 12);
  const open = organic.filter((c) => c.openNow === true).slice(0, 12);

  return (
    <View style={styles.bg}>
      <HomeHeader />
      <ScrollView
        contentContainerStyle={styles.wrap}
        refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load()} tintColor={colors.coral} />}>
        {isEvening() ? (
          <Pressable
            style={styles.eve}
            onPress={() => {
              filters.setOpenNow(true);
              goDiscover(filters.intent);
            }}
            accessibilityRole="button">
            <Text style={styles.eveText}>Akşam oldu — şu an açık yerlere bak</Text>
          </Pressable>
        ) : null}
        <CategoryGrid selected={filters.intent} onSelect={goDiscover} />
        {sponsored.length > 0 ? (
          <PromoBanner
            onOpenNow={() => {
              filters.setOpenNow(true);
              goDiscover(filters.intent);
            }}
          />
        ) : null}
        {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
        {featured ? (
          <View style={styles.feat}>
            <Text style={styles.gridTitle}>Sana özel</Text>
            <VenueProductCard card={featured} featured intent={filters.intent} />
          </View>
        ) : null}
        <HorizontalVenueRow
          title={`${intentLabel(filters.intent)} · ${filters.label}`}
          subtitle="Kısa liste"
          items={special.slice(featured ? 1 : 0)}
          onSeeAll={() => void expand()}
          intent={filters.intent}
        />
        <HorizontalVenueRow
          title="Şu an açık"
          subtitle="Kapıya git, otur"
          items={open}
          onSeeAll={() => {
            filters.setOpenNow(true);
            goDiscover(filters.intent);
          }}
          intent={filters.intent}
        />
        {!loading && organic.length === 0 && !error ? (
          <EmptyState
            title={emptyNearby(filters.intent, filters.label)}
            action="Yarıçapı büyüt"
            onAction={() => {
              filters.setRadius(filters.radiusMeters * 2);
            }}
          />
        ) : null}
        {showAll ? (
          <View style={styles.list}>
            <Text style={styles.gridTitle}>Yakındakiler</Text>
            {organic.map((card) => (
              <View key={`feed-${card.placeId}`} style={styles.listItem}>
                <VenueProductCard card={card} intent={filters.intent} />
              </View>
            ))}
          </View>
        ) : null}
        {organic.length > 0 && (!showAll || hasMore) ? (
          <Pressable
            style={styles.more}
            onPress={() => void expand()}
            disabled={loadingMore}
            accessibilityRole="button">
            <Text style={styles.moreText}>
              {loadingMore ? 'Yükleniyor…' : `Daha fazla ${intentLabel(filters.intent).toLowerCase()}`}
            </Text>
          </Pressable>
        ) : null}
      </ScrollView>
      <SearchingOverlay visible={loading} intent={filters.intent} />
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingBottom: 48 },
  eve: {
    marginHorizontal: 20,
    marginBottom: 8,
    backgroundColor: colors.espresso,
    borderRadius: 16,
    paddingVertical: 12,
    paddingHorizontal: 14,
  },
  eveText: { color: colors.white, fontWeight: '700', fontSize: 13 },
  feat: { paddingHorizontal: 20, marginTop: 20 },
  gridTitle: {
    marginBottom: 12,
    fontFamily: fonts.display,
    fontSize: 24,
    color: colors.ink,
  },
  more: { marginHorizontal: 20, marginTop: 20, alignItems: 'center', padding: 14, minHeight: 44 },
  moreText: { color: colors.coral, fontWeight: '800' },
  list: { paddingHorizontal: 20, marginTop: 20 },
  listItem: { marginBottom: 16 },
});
