import { VenueImage } from '@/components/VenueImage';
import type { ListItem } from '@/lib/collections';
import { colors, fonts, radii, shadows } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';

/** Listedeki mekan satırı: dokununca mekan sayfası; onRemove verilirse kaldırma düğmesi. */
export function ListItemRow({ item, onRemove }: { item: ListItem; onRemove?: () => void }) {
  const router = useRouter();
  return (
    <View style={styles.row}>
      <Pressable
        style={styles.main}
        onPress={() => router.push({ pathname: '/venue/[id]', params: { id: item.placeId, name: item.name, photo: item.photoUrl ?? '' } })}
        accessibilityRole="button"
        accessibilityLabel={`${item.name}${item.category ? `, ${item.category}` : ''}`}>
        <View style={styles.thumb}>
          <VenueImage placeId={item.placeId} photoUrl={item.photoUrl} name={item.name} style={StyleSheet.absoluteFill} radius={radii.sm} widthPt={64} />
        </View>
        <View style={{ flex: 1 }}>
          <Text style={styles.name} numberOfLines={1}>
            {item.name}
          </Text>
          {item.category ? <Text style={styles.sub}>{item.category}</Text> : null}
        </View>
      </Pressable>
      {onRemove ? (
        <Pressable onPress={onRemove} style={styles.remove} accessibilityRole="button" accessibilityLabel={`${item.name} listeden çıkar`}>
          <Ionicons name="remove-circle-outline" size={22} color={colors.muted} />
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 10,
    padding: 10,
    borderRadius: radii.md,
    backgroundColor: colors.surface,
    ...shadows.card,
  },
  main: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 12, minHeight: 64 },
  thumb: { width: 64, height: 64, borderRadius: radii.sm, overflow: 'hidden', backgroundColor: colors.line },
  name: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 16 },
  sub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12, marginTop: 2 },
  remove: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center' },
});
