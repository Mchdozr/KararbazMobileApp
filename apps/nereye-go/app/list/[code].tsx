import { EmptyState } from '@/components/EmptyState';
import { ListItemRow } from '@/components/ListItemRow';
import { Skeleton } from '@/components/Skeleton';
import { api } from '@/lib/api';
import type { PublicList } from '@/lib/collections';
import { friendlyError } from '@/lib/empty';
import { colors, fonts, radii } from '@/lib/theme';
import { useLocalSearchParams } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/** Bağlantıyla paylaşılan liste (salt okunur). Sahibinin kimliği gösterilmez. */
export default function SharedList() {
  const { code } = useLocalSearchParams<{ code: string }>();
  const insets = useSafeAreaInsets();
  const [list, setList] = useState<PublicList | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setList(await api.publicList(code!));
    } catch (e) {
      setError(friendlyError(e));
    }
  }, [code]);

  useEffect(() => {
    void load();
  }, [load]);

  if (error) {
    return <EmptyState icon="link-outline" title="Liste açılamadı" body={error} action="Tekrar dene" onAction={() => void load()} />;
  }
  if (!list) {
    return (
      <View style={[styles.bg, styles.wrap]} accessibilityLabel="Liste yükleniyor">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} height={84} radius={radii.md} style={{ marginTop: 10 }} />
        ))}
      </View>
    );
  }
  return (
    <ScrollView style={styles.bg} contentContainerStyle={[styles.wrap, { paddingBottom: insets.bottom + 32 }]}>
      <Text style={styles.title}>{list.name}</Text>
      <Text style={styles.sub}>{list.items.length} mekan · paylaşılan liste</Text>
      {list.items.map((item) => (
        <ListItemRow key={item.placeId} item={item} />
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 20 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 26 },
  sub: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 13, marginTop: 2, marginBottom: 6 },
});
