import { EmptyState } from '@/components/EmptyState';
import { Skeleton } from '@/components/Skeleton';
import { api } from '@/lib/api';
import { friendlyError } from '@/lib/empty';
import { MAP_ATTRIBUTION } from '@/lib/map-config';
import { colors, fonts, shadows } from '@/lib/theme';
import type { Legal as LegalData } from '@/lib/types';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/**
 * Gizlilik (KVKK) ve kullanım koşulları. Metin uygulamada gömülü değil: sunucudaki /api/legal'dan gelir,
 * aynı metin /gizlilik ve /kosullar sayfalarında da durur. İletişim adresi yalnızca sunucu ayarından gelir.
 */
export default function Legal() {
  const insets = useSafeAreaInsets();
  const { doc } = useLocalSearchParams<{ doc?: string }>();
  const terms = doc === 'terms';
  const [data, setData] = useState<LegalData | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    setError(null);
    api
      .legal()
      .then(setData)
      .catch((e) => setError(friendlyError(e)));
  }, []);

  useEffect(load, [load]);

  const page = data ? (terms ? data.terms : data.privacy) : null;

  return (
    <ScrollView style={styles.bg} contentContainerStyle={[styles.wrap, { paddingBottom: insets.bottom + 32 }]}>
      <Stack.Screen options={{ title: terms ? 'Kullanım koşulları' : 'Gizlilik ve KVKK' }} />
      {error ? (
        <EmptyState icon="cloud-offline-outline" title="Metin yüklenemedi" body={error} action="Tekrar dene" onAction={load} />
      ) : !page ? (
        <View style={styles.loading} accessibilityLabel="Yükleniyor">
          <Skeleton height={34} width="70%" />
          <Skeleton height={18} />
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} height={96} radius={18} />
          ))}
        </View>
      ) : (
        <>
          <Text style={styles.lead}>{page.lead}</Text>
          {page.sections.map((s) => (
            <View key={s.title} style={styles.card}>
              <Text style={styles.title}>{s.title}</Text>
              <Text style={styles.body}>{s.body}</Text>
            </View>
          ))}
          {data?.draft ? <Text style={styles.foot}>{data.draft}</Text> : null}
          <Text style={styles.foot}>{MAP_ATTRIBUTION}</Text>
        </>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 20, gap: 12 },
  loading: { gap: 12 },
  h: { fontFamily: fonts.display, fontSize: 30, lineHeight: 36, color: colors.ink },
  lead: { color: colors.muted, fontSize: 15, lineHeight: 22, fontFamily: fonts.sans, marginBottom: 4 },
  card: {
    backgroundColor: colors.surface,
    borderRadius: 20,
    padding: 18,
    gap: 6,
    ...shadows.card,
  },
  title: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 15 },
  body: { color: colors.ink, opacity: 0.86, fontSize: 14, lineHeight: 21, fontFamily: fonts.sans },
  foot: { color: colors.muted, fontSize: 12, marginTop: 8, textAlign: 'center' },
});
