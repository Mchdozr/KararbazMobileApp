import { t } from '@/lib/i18n';
import { Button } from '@/components/Button';
import { EmptyState } from '@/components/EmptyState';
import { Skeleton } from '@/components/Skeleton';
import { TextField } from '@/components/TextField';
import { VenueImage } from '@/components/VenueImage';
import { api } from '@/lib/api';
import { cleanListName, LIST_NAME_MAX, type ListSummary } from '@/lib/collections';
import { friendlyError } from '@/lib/empty';
import { useSession } from '@/lib/session';
import { colors, fonts, radii, shadows } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/** Listelerim: adlandırılmış listeler ve yeni liste. */
export default function Collections() {
  const { session } = useSession();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [lists, setLists] = useState<ListSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setLists(await api.collections());
      setError(null);
    } catch (e) {
      setError(friendlyError(e));
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      if (!session) {
        router.replace('/auth');
        return;
      }
      void load();
    }, [session, router, load]),
  );

  async function create() {
    const clean = cleanListName(name);
    if (!clean) {
      return;
    }
    setBusy(true);
    try {
      const created = await api.createCollection(clean);
      setName('');
      router.push({ pathname: '/collections/[id]', params: { id: created.id } });
    } catch (e) {
      toast.error(friendlyError(e));
    } finally {
      setBusy(false);
    }
  }

  if (error && !lists) {
    return <EmptyState icon="bookmarks-outline" title={t('Listeler açılamadı')} body={error} action={t('Tekrar dene')} onAction={() => void load()} />;
  }

  return (
    <ScrollView style={styles.bg} contentContainerStyle={[styles.wrap, { paddingBottom: insets.bottom + 32 }]} keyboardShouldPersistTaps="handled">
      {lists === null ? (
        [0, 1, 2].map((i) => <Skeleton key={i} height={84} radius={radii.md} style={{ marginTop: 10 }} />)
      ) : lists.length === 0 ? (
        <Text style={styles.empty}>{t('Henüz listen yok. "Kahve rotam", "Hafta sonu" gibi bir liste oluştur; mekan sayfasından "Listeye ekle" ile doldur.')}</Text>
      ) : (
        lists.map((l) => (
          <Pressable
            key={l.id}
            style={styles.card}
            onPress={() => router.push({ pathname: '/collections/[id]', params: { id: l.id } })}
            accessibilityRole="button"
            accessibilityLabel={t('{p0}, {p1} mekan{p2}', { p0: l.name, p1: l.count, p2: l.shared ? t(', paylaşılıyor') : '' })}>
            <View style={styles.cover}>
              {l.coverUrl ? (
                <VenueImage placeId={`list:${l.id}`} photoUrl={l.coverUrl} name={l.name} style={StyleSheet.absoluteFill} radius={radii.sm} widthPt={72} />
              ) : (
                <Ionicons name="bookmarks-outline" size={26} color={colors.coral} />
              )}
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.name} numberOfLines={1}>
                {l.name}
              </Text>
              <Text style={styles.sub}>
                {l.count} mekan{l.shared ? t(' · paylaşılıyor') : ''}
              </Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.muted} />
          </Pressable>
        ))
      )}
      <View style={styles.newBox}>
        <TextField label={t('Yeni liste')} value={name} onChangeText={setName} maxLength={LIST_NAME_MAX} placeholder={t('Kahve rotam')} />
        <Button label={t('Liste oluştur')} icon="add" loading={busy} disabled={!cleanListName(name)} onPress={() => void create()} />
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 20 },
  empty: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20 },
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    marginTop: 10,
    padding: 10,
    minHeight: 84,
    borderRadius: radii.md,
    backgroundColor: colors.surface,
    ...shadows.card,
  },
  cover: {
    width: 64,
    height: 64,
    borderRadius: radii.sm,
    overflow: 'hidden',
    backgroundColor: colors.coralSoft,
    alignItems: 'center',
    justifyContent: 'center',
  },
  name: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 16 },
  sub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13, marginTop: 2 },
  newBox: { marginTop: 24, gap: 10 },
});
