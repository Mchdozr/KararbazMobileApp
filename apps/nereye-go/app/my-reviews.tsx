import { EmptyState } from '@/components/EmptyState';
import { Skeleton } from '@/components/Skeleton';
import { Stars } from '@/components/Stars';
import { api } from '@/lib/api';
import { vibeText } from '@/lib/constants';
import { friendlyError } from '@/lib/empty';
import { relativeDate } from '@/lib/reviews';
import { useSession } from '@/lib/session';
import { colors, fonts } from '@/lib/theme';
import { toast } from '@/lib/toast';
import type { MyReview } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Alert, FlatList, Pressable, StyleSheet, Text, View } from 'react-native';

export default function MyReviews() {
  const { session } = useSession();
  const router = useRouter();
  const [items, setItems] = useState<MyReview[] | null>(null);

  useFocusEffect(
    useCallback(() => {
      if (!session) {
        setItems([]);
        return;
      }
      let alive = true;
      void api.myReviews().then((r) => {
        if (alive) {
          setItems(r);
        }
      });
      return () => {
        alive = false;
      };
    }, [session]),
  );

  function remove(review: MyReview) {
    Alert.alert('Yorumu sil', `${review.venueName} için yazdığın yorum ve puan kaldırılacak.`, [
      { text: 'Vazgeç', style: 'cancel' },
      {
        text: 'Sil',
        style: 'destructive',
        onPress: () => {
          const before = items;
          setItems((prev) => (prev ?? []).filter((r) => r.id !== review.id));
          api
            .deleteReview(review.id)
            .then(() => toast.success('Yorumun silindi'))
            .catch((e) => {
              setItems(before);
              toast.error(friendlyError(e));
            });
        },
      },
    ]);
  }

  if (items === null) {
    return (
      <View style={[styles.bg, styles.wrap]} accessibilityLabel="Yükleniyor">
        <Skeleton height={110} radius={18} />
        <Skeleton height={110} radius={18} />
      </View>
    );
  }

  return (
    <FlatList
      style={styles.bg}
      data={items}
      keyExtractor={(r) => r.id}
      contentContainerStyle={styles.wrap}
      ListEmptyComponent={
        <EmptyState
          icon="chatbubble-ellipses-outline"
          title="Henüz yorum yazmadın"
          body="Gittiğin bir mekanı değerlendir; sıradaki kişinin kararını kolaylaştır."
          action="Mekan bul"
          onAction={() => router.replace('/(tabs)')}
        />
      }
      renderItem={({ item }) => (
        <Pressable
          style={styles.card}
          onPress={() => router.push({ pathname: '/venue/[id]', params: { id: item.placeId, name: item.venueName } })}
          accessibilityRole="button"
          accessibilityLabel={`${item.venueName}, ${item.stars} yıldız`}>
          <View style={styles.head}>
            <View style={{ flex: 1 }}>
              <Text style={styles.name} numberOfLines={1}>
                {item.venueName}
              </Text>
              <View style={styles.meta}>
                <Stars value={item.stars} size={13} />
                <Text style={styles.when}>{relativeDate(item.createdAt)}</Text>
              </View>
            </View>
            <Pressable
              onPress={() => router.push({ pathname: '/review/[id]', params: { id: item.placeId } })}
              hitSlop={8}
              style={styles.icon}
              accessibilityRole="button"
              accessibilityLabel="Yorumu düzenle">
              <Ionicons name="create-outline" size={20} color={colors.coral} />
            </Pressable>
            <Pressable onPress={() => remove(item)} hitSlop={8} style={styles.icon} accessibilityRole="button" accessibilityLabel="Yorumu sil">
              <Ionicons name="trash-outline" size={20} color={colors.danger} />
            </Pressable>
          </View>
          {item.text ? (
            <Text style={styles.text} numberOfLines={4}>
              {item.text}
            </Text>
          ) : null}
          {item.tags.length ? <Text style={styles.tags}>{item.tags.map(vibeText).join(' · ')}</Text> : null}
          {item.ownerReply ? (
            <View style={styles.reply}>
              <Text style={styles.replyWho}>İşletmenin yanıtı</Text>
              <Text style={styles.text}>{item.ownerReply}</Text>
            </View>
          ) : null}
        </Pressable>
      )}
    />
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 20, gap: 12, flexGrow: 1 },
  card: { backgroundColor: colors.surface, borderRadius: 18, borderWidth: 1, borderColor: colors.line, padding: 16, gap: 8 },
  head: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  name: { fontFamily: fonts.display, fontSize: 19, color: colors.ink },
  meta: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 4 },
  when: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12 },
  icon: { width: 40, height: 40, alignItems: 'center', justifyContent: 'center' },
  text: { color: colors.ink, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20 },
  tags: { color: colors.muted, fontFamily: fonts.sansMed, fontSize: 12 },
  reply: { borderLeftWidth: 3, borderLeftColor: colors.gold, paddingLeft: 10, gap: 2 },
  replyWho: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 12 },
});
