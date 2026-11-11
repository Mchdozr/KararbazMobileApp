import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { CategoryGrid } from '@/components/home/CategoryGrid';
import { HomeHeader } from '@/components/home/HomeHeader';
import { HorizontalVenueRow } from '@/components/home/HorizontalVenueRow';
import { PromoBanner } from '@/components/home/PromoBanner';
import { VenueProductCard } from '@/components/VenueProductCard';
import { isEvening } from '@/lib/copy';
import { INTENTS } from '@/lib/constants';
import { emptyNearby, friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { api } from '@/lib/api';
import { colors, fonts } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

export default function Home() {
  const filters = useFilters();
  const router = useRouter();
  const [items, setItems] = useState<VenueCard[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [showAll, setShowAll] = useState(true);
  const skipRef = useRef(0);

  const fetchMerged = useCallback(async (skip: number) => {
    let firstError: unknown = null;
    const pages = await Promise.all(
      INTENTS.map(async (intent) => {
        try {
          const res = await api.recommend(
            filters.lat,
            filters.lng,
            intent,
            filters.openNow,
            filters.vibes,
            filters.radiusMeters,
            skip,
            40,
          );
          return { intent, items: res.items, hasMore: res.hasMore, ok: true };
        } catch (error) {
          firstError = firstError ?? error;
          return { intent, items: [] as VenueCard[], hasMore: false, ok: false };
        }
      }),
    );
    if (pages.every((page) => !page.ok)) {
      throw firstError ?? new Error('network');
    }
    const seen = new Set<string>();
    const next: VenueCard[] = [];
    for (const page of pages) {
      for (const card of page.items) {
        if (seen.has(card.placeId)) {
          continue;
        }
        if (filters.minRating && (card.nereyeRating ?? 0) < filters.minRating) {
          continue;
        }
        seen.add(card.placeId);
        next.push({ ...card, intent: page.intent });
      }
    }
    next.sort((a, b) => a.distanceMeters - b.distanceMeters);
    return { items: next, hasMore: pages.some((page) => page.hasMore) };
  }, [filters.lat, filters.lng, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const merged = await fetchMerged(0);
      skipRef.current = 40;
      setItems(merged.items);
      setHasMore(merged.hasMore);
    } catch (e) {
      setError(friendlyError(e));
      setItems([]);
      setHasMore(false);
    } finally {
      setLoading(false);
    }
  }, [fetchMerged]);

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
      const merged = await fetchMerged(skipRef.current);
      skipRef.current += 40;
      setItems((prev) => {
        const seen = new Set(prev.map((c) => c.placeId));
        return [...prev, ...merged.items.filter((c) => !seen.has(c.placeId))];
      });
      setHasMore(merged.hasMore);
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
            <VenueProductCard card={featured} featured intent={featured.intent ?? filters.intent} />
          </View>
        ) : null}
        <HorizontalVenueRow
          title="Yakınındaki mekanlar"
          subtitle={`${filters.label} · kahve, yemek, gece`}
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
                <VenueProductCard card={card} intent={card.intent ?? filters.intent} />
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
              {loadingMore ? 'Yükleniyor…' : 'Daha fazla mekan'}
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
