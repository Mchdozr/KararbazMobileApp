import { t } from '@/lib/i18n';
import { EmptyState } from '@/components/EmptyState';
import { VenueGridSkeleton } from '@/components/Skeleton';
import { VenueProductCard } from '@/components/VenueProductCard';
import { loadFavorites, useFavorites } from '@/lib/favorites';
import { useSession } from '@/lib/session';
import { colors } from '@/lib/theme';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';

export default function Favorites() {
  const { session } = useSession();
  const router = useRouter();
  const favorites = useFavorites();
  const [refreshing, setRefreshing] = useState(false);

  if (!session) {
    return (
      <View style={styles.bg}>
        <EmptyState
          icon="heart-outline"
          title={t('Beğendiğin mekanları burada topla')}
          body={t('Giriş yapınca kaydettiklerin her cihazında seninle olur.')}
          action={t('Giriş yap')}
          onAction={() => router.push('/auth')}
        />
      </View>
    );
  }

  if (favorites.loading && favorites.cards.length === 0) {
    return (
      <View style={[styles.bg, { paddingTop: 20 }]}>
        <VenueGridSkeleton count={4} />
      </View>
    );
  }

  return (
    <FlatList
      style={styles.bg}
      data={favorites.cards}
      keyExtractor={(c) => c.placeId}
      numColumns={2}
      initialNumToRender={6}
      maxToRenderPerBatch={6}
      windowSize={7}
      removeClippedSubviews
      columnWrapperStyle={styles.column}
      contentContainerStyle={styles.wrap}
      renderItem={({ item }) => (
        <View style={styles.cell}>
          <VenueProductCard card={{ ...item, distanceMeters: -1 }} />
        </View>
      )}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          tintColor={colors.coral}
          colors={[colors.coral]}
          onRefresh={() => {
            setRefreshing(true);
            void loadFavorites(session.userId, true)
              .catch(() => {})
              .finally(() => setRefreshing(false));
          }}
        />
      }
      ListEmptyComponent={
        <EmptyState
          icon="heart-outline"
          title={t('Henüz kaydettiğin mekan yok')}
          body={t('Bir mekanın üstündeki kalbe dokun, burada birikir.')}
          action={t('Keşfet')}
          onAction={() => router.replace('/(tabs)')}
        />
      }
    />
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingTop: 20, paddingBottom: 48, flexGrow: 1 },
  column: { paddingHorizontal: 20, gap: 12, marginBottom: 16 },
  cell: { flex: 1, maxWidth: '50%' },
});
