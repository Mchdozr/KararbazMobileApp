import { t } from '@/lib/i18n';
import { Button } from '@/components/Button';
import { EmptyState } from '@/components/EmptyState';
import { ListItemRow } from '@/components/ListItemRow';
import { Skeleton } from '@/components/Skeleton';
import { TextField } from '@/components/TextField';
import { api } from '@/lib/api';
import { cleanListName, LIST_NAME_MAX, listShareText, type ListDetail } from '@/lib/collections';
import { friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { colors, fonts, radii } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { Alert, ScrollView, Share, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/** Bir liste: mekanlar, paylaşım bağlantısı, yeniden adlandırma ve silme. */
export default function CollectionScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [list, setList] = useState<ListDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const l = await api.collection(id!);
      setList(l);
      setName(l.name);
      setError(null);
    } catch (e) {
      setError(friendlyError(e));
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function run(key: string, work: () => Promise<void>) {
    setBusy(key);
    try {
      await work();
    } catch (e) {
      toast.error(friendlyError(e));
    } finally {
      setBusy(null);
    }
  }

  async function share() {
    await run('share', async () => {
      const s = await api.shareCollection(id!);
      setList((prev) => (prev ? { ...prev, share: s } : prev));
      haptic.success();
      try {
        await Share.share({ message: listShareText(list!.name, list!.items.map((i) => i.name), s), title: list!.name });
      } catch {
        /* vazgeçildi */
      }
    });
  }

  function confirmDelete() {
    Alert.alert('Listeyi sil', t('"{p0}" ve içindeki mekan kayıtları silinsin mi? Paylaşım bağlantısı da çalışmaz olur.', { p0: list?.name ?? '' }), [
      { text: t('Vazgeç'), style: 'cancel' },
      {
        text: t('Sil'),
        style: 'destructive',
        onPress: () =>
          void run('delete', async () => {
            await api.deleteCollection(id!);
            toast.success(t('Liste silindi'));
            router.back();
          }),
      },
    ]);
  }

  if (error && !list) {
    return <EmptyState icon="bookmarks-outline" title={t('Liste açılamadı')} body={error} action={t('Tekrar dene')} onAction={() => void load()} />;
  }
  if (!list) {
    return (
      <View style={[styles.bg, styles.wrap]} accessibilityLabel={t('Liste yükleniyor')}>
        <Skeleton height={28} width="60%" />
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} height={84} radius={radii.md} style={{ marginTop: 10 }} />
        ))}
      </View>
    );
  }

  const renamed = cleanListName(name);
  return (
    <ScrollView style={styles.bg} contentContainerStyle={[styles.wrap, { paddingBottom: insets.bottom + 32 }]} keyboardShouldPersistTaps="handled">
      <Text style={styles.title}>{list.name}</Text>
      <Text style={styles.sub}>
        {list.items.length} mekan{list.share ? t(' · bağlantıyla paylaşılıyor') : ''}
      </Text>
      {list.items.length === 0 ? (
        <Text style={styles.empty}>{t('Liste boş. Mekan sayfasındaki "Listeye ekle" ile doldur.')}</Text>
      ) : (
        list.items.map((item) => (
          <ListItemRow
            key={item.placeId}
            item={item}
            onRemove={() =>
              void run(`rm:${item.placeId}`, async () => {
                await api.removeFromCollection(list.id, item.placeId);
                setList((prev) => (prev ? { ...prev, items: prev.items.filter((i) => i.placeId !== item.placeId) } : prev));
                toast.info(t('{p0} listeden çıkarıldı', { p0: item.name }));
              })
            }
          />
        ))
      )}

      <View style={styles.actions}>
        <Button label={t('Bağlantıyla paylaş')} icon="share-outline" loading={busy === 'share'} disabled={list.items.length === 0} onPress={() => void share()} />
        {list.share ? (
          <Button
            label={t('Paylaşımı durdur')}
            variant="secondary"
            loading={busy === 'unshare'}
            onPress={() =>
              void run('unshare', async () => {
                await api.unshareCollection(list.id);
                setList((prev) => (prev ? { ...prev, share: null } : prev));
                toast.info(t('Bağlantı artık çalışmıyor'));
              })
            }
          />
        ) : null}
        <TextField label={t('Liste adı')} value={name} onChangeText={setName} maxLength={LIST_NAME_MAX} />
        <Button
          label={t('Adı kaydet')}
          variant="outline"
          loading={busy === 'rename'}
          disabled={!renamed || renamed === list.name}
          onPress={() =>
            void run('rename', async () => {
              setList(await api.renameCollection(list.id, renamed!));
              toast.success(t('Liste adı güncellendi'));
            })
          }
        />
        <Button label={t('Listeyi sil')} variant="danger" icon="trash-outline" loading={busy === 'delete'} onPress={confirmDelete} />
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 20 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 26 },
  sub: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 13, marginTop: 2, marginBottom: 6 },
  empty: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20, marginTop: 10 },
  actions: { gap: 10, marginTop: 24 },
});
