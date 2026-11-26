import { t } from '@/lib/i18n';
import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { VenueCardSkeleton, VenueRowSkeleton } from '@/components/Skeleton';
import { CategoryGrid } from '@/components/home/CategoryGrid';
import { AnalyticsPrompt } from '@/components/AnalyticsPrompt';
import { TonightBand } from '@/components/TonightBand';
import { WIDGET_FILE, widgetSnapshot } from '@/lib/widget-data';
import { minutesOfDay } from '@/lib/open-status';
import { WeatherNote } from '@/components/WeatherNote';
import type { WeatherInfo } from '@/lib/weather';
import { DecisionSheet } from '@/components/DecisionSheet';
import { HomeHeader } from '@/components/home/HomeHeader';
import { HorizontalVenueRow } from '@/components/home/HorizontalVenueRow';
import { PromoBanner } from '@/components/home/PromoBanner';
import { VenueProductCard } from '@/components/VenueProductCard';
import { isEvening } from '@/lib/copy';
import { intentLabel } from '@/lib/constants';
import { emptyNearby, friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { useFavorites } from '@/lib/favorites';
import { useFilters } from '@/lib/filters';
import { recentAsCard, useRecentVenues } from '@/lib/recent-venues';
import { api } from '@/lib/api';
import { track } from '@/lib/analytics';
import { useLatestRequest } from '@/lib/use-latest-request';
import { isOffline, offlineBanner, savedListName, toSaved, usableSaved, type SavedList } from '@/lib/offline';
import { advancePage } from '@/lib/paging';
import { peekRecommend, recommendCacheKey } from '@/lib/recommend-cache';
import { getLargeJson, setLargeJson } from '@/lib/storage';
import { colors, fonts, gradients } from '@/lib/theme';
import type { VenueCard, TonightEvent } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View, Platform } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

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
  const [deciding, setDeciding] = useState(false);
  const [tonight, setTonight] = useState<TonightEvent[]>([]);
  const [weather, setWeather] = useState<WeatherInfo | null>(null);
  // Ağ yokken son başarılı liste gösteriliyorsa bandın metni.
  const [offline, setOffline] = useState<string | null>(null);
  const recents = useRecentVenues();
  // Niyet ya da konum değişince önceki isteğin geç gelen cevabı yeni listeyi ezmesin
  // (ör. Yemek cevabı, Kültür seçildikten sonra gelip Kültür başlığı altına yemek mekanı koymasın).
  const seq = useRef(0);
  // Yeni yükleme başlayınca öncekinin isteği ağda da durur (yalnızca cevabını yok saymak yetmez).
  const nextRequest = useLatestRequest();
  // Sunucudan şimdiye kadar alınan kayıt sayısı (filtreden önce). "Daha fazla" buradan devam eder.
  const offset = useRef(0);
  const favorites = useFavorites();
  const insets = useSafeAreaInsets();

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
    // İstek hemen yola çıkar; beklerken (soğuk açılış, yavaş ağ) aynı semt ve niyetin diskteki son listesi
    // anında çizilir, taze liste gelince yerini alır. Uydurma veri yok: kayıt yoksa iskelet görünür.
    let settled = false;
    const fetching = api.recommend(
      filters.lat,
      filters.lng,
      filters.intent,
      filters.openNow,
      filters.vibes,
      filters.radiusMeters,
      0,
      20,
      { signal: nextRequest() },
    );
    void fetching.then(
      () => {
        settled = true;
      },
      () => {
        settled = true;
      },
    );
    if (!cached) {
      const saved = usableSaved(await getLargeJson<SavedList>(savedListName(filters.intent)), filters.intent);
      if (!settled && n === seq.current && saved && saved.label === filters.label) {
        setItems(saved.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating));
        setHasMore(false);
        setLoading(false);
      }
    }
    try {
      const res = await fetching;
      if (n !== seq.current) {
        return;
      }
      const page = advancePage(0, res, filters.minRating);
      offset.current = page.nextSkip;
      setWeather(res.weather ?? null);
      track('list_view', { intent: filters.intent });
      setItems(page.cards);
      setHasMore(page.hasMore);
      if (res.items.length > 0) {
        void setLargeJson(savedListName(filters.intent), toSaved(filters.intent, filters.label, res.items));
      }
      // Android ana ekran bileşeni bu dosyayı okur (plugins/withNearbyWidget.js).
      if (Platform.OS === 'android') {
        void setLargeJson(
          WIDGET_FILE,
          widgetSnapshot(res.items, { label: filters.label, intent: filters.intent, now: Date.now(), nowMinutes: minutesOfDay() }),
        );
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
          setOffline(t('Çevrimdışısın. Son yüklenen liste gösteriliyor; bilgiler güncel olmayabilir.'));
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
  }, [filters.lat, filters.lng, filters.intent, filters.label, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating, nextRequest]);

  useEffect(() => {
    void load();
  }, [load]);

  // Bu akşamın etkinlikleri listeden bağımsız yüklenir; gelmezse bant görünmez.
  useEffect(() => {
    let alive = true;
    void api.tonight(filters.lat, filters.lng, filters.radiusMeters).then((events) => {
      if (alive) {
        setTonight(events);
      }
    });
    return () => {
      alive = false;
    };
  }, [filters.lat, filters.lng, filters.radiusMeters]);

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
      {/* Durum çubuğunun arkası: üst menü kayıp gidince saat ve pil ikonları açık zeminde kaybolmasın. */}
      <View style={[styles.statusStrip, { height: insets.top, backgroundColor: (isEvening() ? gradients.evening : gradients.hero)[0] }]} />
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
            <HomeHeader topInset={0} />
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
                accessibilityRole="button"
                accessibilityLabel={t('Akşam oldu, şu an açık yerleri göster')}>
                <View style={styles.eveIcon}>
                  <Ionicons name="moon" size={16} color={colors.accent} />
                </View>
                <Text style={styles.eveText}>{t('Akşam oldu — şu an açık yerlere bak')}</Text>
                <Ionicons name="chevron-forward" size={16} color={colors.white} />
              </Pressable>
            ) : null}
            <CategoryGrid selected={filters.intent} onSelect={goDiscover} />
            {organic.length > 0 ? (
              <Pressable
                style={styles.decide}
                onPress={() => {
                  haptic.press();
                  setDeciding(true);
                }}
                accessibilityRole="button"
                accessibilityLabel={t('Benim için seç: şu an için en uygun mekanı gerekçeleriyle göster')}>
                <View style={styles.decideIcon}>
                  <Ionicons name="sparkles" size={18} color={colors.accent} />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.decideTitle}>{t('Benim için seç')}</Text>
                  <Text style={styles.decideSub}>{t('Kararsız mısın? Şu an için en iyisini seçeyim.')}</Text>
                </View>
                <Ionicons name="chevron-forward" size={18} color={colors.onCoral} />
              </Pressable>
            ) : null}
            <WeatherNote weather={weather} onSuggest={goDiscover} />
            <TonightBand events={tonight} />
            <AnalyticsPrompt sawList={organic.length > 0} />
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
              <View accessibilityLabel={t('Öneriler yükleniyor')}>
                <View style={styles.feat}>
                  <Text style={styles.gridTitle}>{t('Sana özel')}</Text>
                  <VenueCardSkeleton photoHeight={210} />
                </View>
                <View style={styles.skelRow}>
                  <VenueRowSkeleton />
                </View>
              </View>
            ) : null}
            {featured ? (
              <View style={styles.feat}>
                <Text style={styles.gridTitle}>{t('Sana özel')}</Text>
                <VenueProductCard card={featured} featured intent={filters.intent} loadPhoto />
              </View>
            ) : null}
            <HorizontalVenueRow
              title={`${intentLabel(filters.intent)} · ${filters.label}`}
              subtitle={t('Kısa liste')}
              items={special.slice(featured ? 1 : 0)}
              onSeeAll={() => void expand()}
              intent={filters.intent}
            />
            <HorizontalVenueRow
              title={t('Şu an açık')}
              subtitle={t('Kapıya git, otur')}
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
                action={t('Yarıçapı büyüt')}
                onAction={() => {
                  filters.setRadius(filters.radiusMeters * 2);
                }}
                secondary={t('Konumu değiştir')}
                onSecondary={() => router.push('/places')}
              />
            ) : null}
            <HorizontalVenueRow
              title={t('Kaydettiklerin')}
              items={favorites.cards}
              onSeeAll={() => router.push('/favorites')}
              compact
            />
            <HorizontalVenueRow title={t('Son baktıkların')} items={recentCards} compact />
            {showAll && organic.length > 0 ? <Text style={[styles.gridTitle, styles.listTitle]}>{t('Yakındakiler')}</Text> : null}
          </>
        }
        ListFooterComponent={
          organic.length > 0 && (!showAll || hasMore) && !offline ? (
            <Pressable
              style={styles.more}
              onPress={() => void expand()}
              disabled={loadingMore}
              accessibilityRole="button"
              accessibilityLabel={t('Daha fazla {p0} göster', { p0: intentLabel(filters.intent).toLowerCase() })}>
              <Text style={styles.moreText}>
                {loadingMore ? t('Yükleniyor…') : t('Daha fazla {p0}', { p0: intentLabel(filters.intent).toLowerCase() })}
              </Text>
            </Pressable>
          ) : undefined
        }
      />
      <SearchingOverlay visible={loading && !first} intent={filters.intent} />
      <DecisionSheet
        visible={deciding}
        cards={organic}
        intentLabel={`${intentLabel(filters.intent)} · ${filters.label}`}
        origin={{ lat: filters.lat, lng: filters.lng }}
        onClose={() => setDeciding(false)}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  statusStrip: { width: '100%' },
  wrap: { paddingBottom: 48 },
  eve: {
    marginHorizontal: 20,
    marginTop: 16,
    backgroundColor: colors.espresso,
    borderRadius: 18,
    paddingVertical: 10,
    paddingHorizontal: 12,
    minHeight: 52,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  eveIcon: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: colors.glassLight,
    alignItems: 'center',
    justifyContent: 'center',
  },
  eveText: { flex: 1, color: colors.white, fontFamily: fonts.sansBold, fontSize: 14 },
  skelRow: { marginTop: 26 },
  decide: {
    marginHorizontal: 20,
    marginTop: 18,
    minHeight: 64,
    borderRadius: 20,
    paddingHorizontal: 14,
    paddingVertical: 12,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    backgroundColor: colors.coral,
  },
  decideIcon: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
  },
  decideTitle: { color: colors.onCoral, fontFamily: fonts.display, fontSize: 18 },
  decideSub: { color: colors.onCoral, opacity: 0.82, fontFamily: fonts.sans, fontSize: 13, marginTop: 1 },
  feat: { paddingHorizontal: 20, marginTop: 20 },
  gridTitle: {
    marginBottom: 12,
    fontFamily: fonts.display,
    fontSize: 22,
    color: colors.ink,
  },
  more: {
    marginHorizontal: 20,
    marginTop: 24,
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 50,
    borderRadius: 16,
    borderWidth: 1.5,
    borderColor: colors.coral,
    backgroundColor: colors.surface,
  },
  moreText: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 15 },
  listTitle: { paddingHorizontal: 20, marginTop: 20 },
  listItem: { paddingHorizontal: 20, marginBottom: 16 },
  offline: {
    marginHorizontal: 20,
    marginTop: 16,
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
