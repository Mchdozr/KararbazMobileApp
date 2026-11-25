import { Button } from '@/components/Button';
import { TextField } from '@/components/TextField';
import { api } from '@/lib/api';
import { cleanListName, LIST_NAME_MAX, type ListItem, type ListSummary } from '@/lib/collections';
import { friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { colors, fonts, radii, shadows } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { Ionicons } from '@expo/vector-icons';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/** Mekan sayfasından "Listeye ekle": listelerde işaretle/kaldır, yeni liste oluştur. Oturum gerektirir. */
export function AddToListSheet({ visible, item, onClose }: { visible: boolean; item: ListItem; onClose: () => void }) {
  const insets = useSafeAreaInsets();
  const [lists, setLists] = useState<ListSummary[] | null>(null);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) {
      return;
    }
    setLists(null);
    void api
      .collections(item.placeId)
      .then(setLists)
      .catch((e) => {
        toast.error(friendlyError(e));
        setLists([]);
      });
  }, [visible, item.placeId]);

  async function toggle(list: ListSummary) {
    setBusy(list.id);
    haptic.tap();
    try {
      if (list.contains) {
        await api.removeFromCollection(list.id, item.placeId);
        toast.info(`"${list.name}" listesinden çıkarıldı`);
      } else {
        await api.addToCollection(list.id, item);
        toast.success(`"${list.name}" listesine eklendi`);
      }
      setLists((prev) =>
        prev?.map((l) => (l.id === list.id ? { ...l, contains: !l.contains, count: l.count + (l.contains ? -1 : 1) } : l)) ?? prev,
      );
    } catch (e) {
      toast.error(friendlyError(e));
    } finally {
      setBusy(null);
    }
  }

  async function create() {
    const clean = cleanListName(name);
    if (!clean) {
      return;
    }
    setBusy('new');
    try {
      const created = await api.createCollection(clean);
      await api.addToCollection(created.id, item);
      haptic.success();
      toast.success(`"${clean}" oluşturuldu, ${item.name} eklendi`);
      setName('');
      setLists((prev) => [{ id: created.id, name: clean, count: 1, coverUrl: item.photoUrl, shared: false, contains: true }, ...(prev ?? [])]);
    } catch (e) {
      toast.error(friendlyError(e));
    } finally {
      setBusy(null);
    }
  }

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose} statusBarTranslucent>
      <Pressable style={styles.backdrop} onPress={onClose} accessibilityRole="button" accessibilityLabel="Listeleri kapat" />
      <View style={[styles.sheet, { paddingBottom: insets.bottom + 16 }]} accessibilityViewIsModal>
        <View style={styles.grip} />
        <Text style={styles.title}>Listeye ekle</Text>
        <Text style={styles.sub} numberOfLines={1}>
          {item.name}
        </Text>
        {lists === null ? (
          <ActivityIndicator color={colors.coral} style={{ marginVertical: 24 }} accessibilityLabel="Listeler yükleniyor" />
        ) : (
          <ScrollView style={{ maxHeight: 280 }} contentContainerStyle={{ gap: 8 }}>
            {lists.length === 0 ? <Text style={styles.sub}>Henüz listen yok. Aşağıdan ilkini oluştur.</Text> : null}
            {lists.map((l) => (
              <Pressable
                key={l.id}
                onPress={() => void toggle(l)}
                disabled={busy !== null}
                style={styles.row}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: l.contains, disabled: busy !== null }}
                aria-checked={l.contains}
                accessibilityLabel={`${l.name}, ${l.count} mekan`}>
                <Ionicons name={l.contains ? 'checkmark-circle' : 'ellipse-outline'} size={24} color={l.contains ? colors.coral : colors.muted} />
                <Text style={styles.rowName} numberOfLines={1}>
                  {l.name}
                </Text>
                <Text style={styles.rowCount}>{l.count}</Text>
              </Pressable>
            ))}
          </ScrollView>
        )}
        <View style={styles.newRow}>
          <View style={{ flex: 1 }}>
            <TextField label="Yeni liste" value={name} onChangeText={setName} maxLength={LIST_NAME_MAX} placeholder="Kahve rotam" />
          </View>
        </View>
        <Button
          label="Oluştur ve ekle"
          icon="add"
          loading={busy === 'new'}
          disabled={!cleanListName(name)}
          onPress={() => void create()}
        />
        <Button label="Tamam" variant="ghost" onPress={onClose} />
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(6,29,22,0.55)' },
  sheet: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: colors.bg,
    borderTopLeftRadius: radii.lg,
    borderTopRightRadius: radii.lg,
    paddingHorizontal: 20,
    paddingTop: 10,
    gap: 10,
    ...shadows.sheet,
  },
  grip: { alignSelf: 'center', width: 44, height: 5, borderRadius: 3, backgroundColor: colors.line, marginBottom: 6 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 22 },
  sub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    minHeight: 52,
    paddingHorizontal: 12,
    borderRadius: radii.sm,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
  },
  rowName: { flex: 1, color: colors.ink, fontFamily: fonts.sansMed, fontSize: 15 },
  rowCount: { color: colors.muted, fontFamily: fonts.sansBold, fontSize: 13 },
  newRow: { flexDirection: 'row', alignItems: 'flex-end', gap: 10 },
});
