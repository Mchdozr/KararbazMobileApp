import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { VenueCardSkeleton, VenueRowSkeleton } from '@/components/Skeleton';
import { CategoryGrid } from '@/components/home/CategoryGrid';
import { HomeHeader } from '@/components/home/HomeHeader';
import { HorizontalVenueRow } from '@/components/home/HorizontalVenueRow';
import { PromoBanner } from '@/components/home/PromoBanner';
import { VenueProductCard } from '@/components/VenueProductCard';
import { isEvening } from '@/lib/copy';
import { intentLabel } from '@/lib/constants';
import { emptyNearby, friendlyError } from '@/lib/empty';
import { useFavorites } from '@/lib/favorites';
import { useFilters } from '@/lib/filters';
import { recentAsCard, useRecentVenues } from '@/lib/recent-venues';
import { api } from '@/lib/api';
import { isOffline, offlineBanner, savedListName, toSaved, usableSaved, type SavedList } from '@/lib/offline';
import { advancePage } from '@/lib/paging';
import { peekRecommend, recommendCacheKey } from '@/lib/recommend-cache';
import { getLargeJson, setLargeJson } from '@/lib/storage';
import { colors, fonts } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';

const NO_CARDS: VenueCard[] = [];

export default function Home() {
  const filters = useFilters();
  const router = useRouter();
  const [items, setItems] = useState<VenueCard[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [showAll, setShowAll] = useState(false);
  // Ağ yokken son başarılı liste gösteriliyorsa bandın metni.
  const [offline, setOffline] = useState<string | null>(null);
  const recents = useRecentVenues();
  // Niyet ya da konum değişince önceki isteğin geç gelen cevabı yeni listeyi ezmesin
  // (ör. Yemek cevabı, Kültür seçildikten sonra gelip Kültür başlığı altına yemek mekanı koymasın).
  const seq = useRef(0);
  // Sunucudan şimdiye kadar alınan kayıt sayısı (filtreden önce). "Daha fazla" buradan devam eder.
  const offset = useRef(0);
  const favorites = useFavorites();

  const load = useCallback(async () => {
    const n = ++seq.current;
    setError(null);
    setOffline(null);
    setShowAll(false);
    const cached = peekRecommend(
      recommendCacheKey({
        lat: filters.lat,
        lng: filters.lng,
        intent: filters.intent,
        radiusMeters: filters.radiusMeters,
        openNow: filters.openNow,
        vibes: filters.vibes,
        skip: 0,
        take: 20,
      }),
    );
    if (cached) {
      const next = cached.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating);
      setItems(next);
      setHasMore(cached.hasMore);
      offset.current = cached.items.length;
      setLoading(false);
    } else {
      setLoading(true);
    }
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
      if (n !== seq.current) {
        return;
      }
      const page = advancePage(0, res, filters.minRating);
      offset.current = page.nextSkip;
      setItems(page.cards);
      setHasMore(page.hasMore);
      if (res.items.length > 0) {
        void setLargeJson(savedListName(filters.intent), toSaved(filters.intent, filters.label, res.items));
      }
    } catch (e) {
      if (n !== seq.current) {
        return;
      }
      if (isOffline(e)) {
        // Ağ yok: bu niyetin son başarılı listesi varsa onu bantla göster. Yoksa hata; veri uydurulmaz.
        const saved = cached ? null : usableSaved(await getLargeJson<SavedList>(savedListName(filters.intent)), filters.intent);
        if (n !== seq.current) {
          return;
        }
        if (saved) {
          setItems(saved.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating));
          setHasMore(false);
          setOffline(offlineBanner(saved));
          return;
        }
        if (cached) {
          setOffline('Çevrimdışısın. Son yüklenen liste gösteriliyor; bilgiler güncel olmayabilir.');
          setHasMore(false);
          return;
        }
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
  }, [filters.lat, filters.lng, filters.intent, filters.label, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating]);

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
    const n = seq.current;
    setLoadingMore(true);
    try {
      const res = await api.recommend(
        filters.lat,
        filters.lng,
        filters.intent,
        filters.openNow,
        filters.vibes,
        filters.radiusMeters,
        offset.current,
        20,
      );
      if (n !== seq.current) {
        return;
      }
      const page = advancePage(offset.current, res, filters.minRating);
      offset.current = page.nextSkip;
      setItems((prev) => {
        const seen = new Set(prev.map((c) => c.placeId));
        return [...prev, ...page.cards.filter((c) => !seen.has(c.placeId))];
      });
      setHasMore(page.hasMore);
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
  const first = loading && items.length === 0;
  const recentCards = useMemo(() => recents.map(recentAsCard), [recents]);

  return (
    <View style={styles.bg}>
      <HomeHeader />
      <FlatList
        data={showAll ? organic : NO_CARDS}
        keyExtractor={(card) => `feed-${card.placeId}`}
        renderItem={({ item }) => (
          <View style={styles.listItem}>
            <VenueProductCard card={item} intent={filters.intent} loadPhoto />
          </View>
        )}
        // Uzun "Daha fazla" listesi pencerelenir: ekranda olmayan kartlar çizilmez, ana sayfa donmaz.
        initialNumToRender={4}
        maxToRenderPerBatch={4}
        windowSize={7}
        removeClippedSubviews
        onEndReachedThreshold={0.6}
        onEndReached={() => {
          if (showAll && hasMore && !loadingMore && !offline) {
            void expand();
          }
        }}
        contentContainerStyle={styles.wrap}
        showsVerticalScrollIndicator={false}
        refreshControl={
          <RefreshControl refreshing={loading && !first} onRefresh={() => void load()} tintColor={colors.coral} colors={[colors.coral]} />
        }
        ListHeaderComponent={
          <>
            {offline ? (
              <View style={styles.offline} accessibilityRole="alert">
                <Ionicons name="cloud-offline-outline" size={18} color={colors.ink} />
                <Text style={styles.offlineText}>{offline}</Text>
              </View>
            ) : null}
            {isEvening() ? (
              <Pressable
                style={styles.eve}
                onPress={() => {
                  filters.setOpenNow(true);
                  goDiscover(filters.intent);
                }}
                accessibilityRole="button">
                <Ionicons name="moon" size={16} color={colors.gold} />
                <Text style={styles.eveText}>Akşam oldu — şu an açık yerlere bak</Text>
                <Ionicons name="chevron-forward" size={16} color={colors.white} />
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
            {first ? (
              <View accessibilityLabel="Öneriler yükleniyor">
                <View style={styles.feat}>
                  <Text style={styles.gridTitle}>Sana özel</Text>
                  <VenueCardSkeleton photoHeight={210} />
                </View>
                <View style={styles.skelRow}>
                  <VenueRowSkeleton />
                </View>
              </View>
            ) : null}
            {featured ? (
              <View style={styles.feat}>
                <Text style={styles.gridTitle}>Sana özel</Text>
                <VenueProductCard card={featured} featured intent={filters.intent} loadPhoto />
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
                icon="map-outline"
                title={emptyNearby(filters.intent, filters.label)}
                action="Yarıçapı büyüt"
                onAction={() => {
                  filters.setRadius(filters.radiusMeters * 2);
                }}
                secondary="Konumu değiştir"
                onSecondary={() => router.push('/places')}
              />
            ) : null}
            <HorizontalVenueRow
              title="Kaydettiklerin"
              items={favorites.cards}
              onSeeAll={() => router.push('/favorites')}
              compact
            />
            <HorizontalVenueRow title="Son baktıkların" items={recentCards} compact />
            {showAll && organic.length > 0 ? <Text style={[styles.gridTitle, styles.listTitle]}>Yakındakiler</Text> : null}
          </>
        }
        ListFooterComponent={
          organic.length > 0 && (!showAll || hasMore) && !offline ? (
            <Pressable
              style={styles.more}
              onPress={() => void expand()}
              disabled={loadingMore}
              accessibilityRole="button"
              accessibilityLabel={`Daha fazla ${intentLabel(filters.intent).toLowerCase()} göster`}>
              <Text style={styles.moreText}>
                {loadingMore ? 'Yükleniyor…' : `Daha fazla ${intentLabel(filters.intent).toLowerCase()}`}
              </Text>
            </Pressable>
          ) : undefined
        }
      />
      <SearchingOverlay visible={loading && !first} intent={filters.intent} />
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
    minHeight: 48,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  eveText: { flex: 1, color: colors.white, fontFamily: fonts.sansBold, fontSize: 14 },
  skelRow: { marginTop: 26 },
  feat: { paddingHorizontal: 20, marginTop: 20 },
  gridTitle: {
    marginBottom: 12,
    fontFamily: fonts.display,
    fontSize: 24,
    color: colors.ink,
  },
  more: { marginHorizontal: 20, marginTop: 20, alignItems: 'center', padding: 14, minHeight: 44 },
  moreText: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 15 },
  listTitle: { paddingHorizontal: 20, marginTop: 20 },
  listItem: { paddingHorizontal: 20, marginBottom: 16 },
  offline: {
    marginHorizontal: 20,
    marginBottom: 8,
    backgroundColor: colors.surface,
    borderColor: colors.line,
    borderWidth: 1,
    borderRadius: 14,
    padding: 12,
    minHeight: 44,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  offlineText: { flex: 1, color: colors.ink, fontFamily: fonts.sans, fontSize: 13, lineHeight: 18 },
});
