import { t } from '@/lib/i18n';
import { Button } from '@/components/Button';
import { EmptyState } from '@/components/EmptyState';
import { Skeleton } from '@/components/Skeleton';
import { VenueImage } from '@/components/VenueImage';
import { api } from '@/lib/api';
import { friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { openDirections } from '@/lib/maps';
import { pollShareText, resultRows, timeLeft, type Poll } from '@/lib/poll';
import { voterKey } from '@/lib/poll-voter';
import { colors, fonts, radii, shadows } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, ScrollView, Share, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/** Arkadaşlarla karar: kısa listeye oy ver, sonuçları canlı izle, kazananın yol tarifini aç. */
export default function PollScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [poll, setPoll] = useState<Poll | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setPoll(await api.poll(code!, await voterKey()));
      setError(null);
    } catch (e) {
      setError(friendlyError(e));
    }
  }, [code]);

  // Ekran açıkken 5 sn'de bir tazelenir: arkadaşların oyları canlı görünür.
  useFocusEffect(
    useCallback(() => {
      void load();
      const t = setInterval(() => void load(), 5000);
      return () => clearInterval(t);
    }, [load]),
  );

  async function vote(placeId: string) {
    if (!poll || poll.expired || busy) {
      return;
    }
    setBusy(placeId);
    haptic.tap();
    try {
      setPoll(await api.vote(poll.code, await voterKey(), placeId));
      haptic.success();
    } catch (e) {
      toast.error(friendlyError(e));
    } finally {
      setBusy(null);
    }
  }

  async function share() {
    if (!poll) {
      return;
    }
    try {
      await Share.share({ message: pollShareText(poll), title: poll.title });
    } catch {
      /* vazgeçildi */
    }
  }

  if (error && !poll) {
    return <EmptyState icon="people-outline" title={t('Oylama açılamadı')} body={error} action={t('Tekrar dene')} onAction={() => void load()} />;
  }

  if (!poll) {
    return (
      <View style={[styles.bg, styles.wrap]} accessibilityLabel={t('Oylama yükleniyor')}>
        <Skeleton height={28} width="70%" />
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} height={84} radius={radii.md} style={{ marginTop: 12 }} />
        ))}
      </View>
    );
  }

  const rows = resultRows(poll);
  const winner = rows.find((r) => r.leading);
  return (
    <ScrollView style={styles.bg} contentContainerStyle={[styles.wrap, { paddingBottom: insets.bottom + 32 }]}>
      <Text style={styles.title}>{poll.title}</Text>
      <Text style={styles.meta}>
        {poll.totalVotes} oy · {timeLeft(poll.expiresAt)}
      </Text>
      <Text style={styles.hint}>
        {poll.expired ? t('Oylama sona erdi.') : poll.myVote ? t('Oyunu değiştirmek için başka mekana dokun.') : t('Bir mekana dokunarak oy ver.')}
      </Text>

      {rows.map((r) => (
        <Pressable
          key={r.placeId}
          onPress={() => void vote(r.placeId)}
          disabled={poll.expired || busy !== null}
          style={[styles.option, r.mine && styles.optionMine]}
          accessibilityRole="button"
          accessibilityState={{ selected: r.mine, disabled: poll.expired }}
          aria-selected={r.mine}
          accessibilityLabel={t('{p0}, {p1} oy{p2}{p3}', { p0: r.name, p1: r.votes, p2: r.mine ? t(', senin oyun') : '', p3: r.leading ? t(', önde') : '' })}>
          <View style={styles.thumb}>
            <VenueImage placeId={r.placeId} photoUrl={r.photoUrl} name={r.name} style={StyleSheet.absoluteFill} radius={radii.sm} widthPt={64} />
          </View>
          <View style={{ flex: 1 }}>
            <View style={styles.optionHead}>
              <Text style={styles.optionName} numberOfLines={1}>
                {r.name}
              </Text>
              {r.leading ? <Ionicons name="trophy" size={16} color={colors.gold} /> : null}
            </View>
            {r.category ? <Text style={styles.optionSub}>{r.category}</Text> : null}
            <View style={styles.track}>
              <View style={[styles.fill, { width: `${r.percent}%` }, r.mine && styles.fillMine]} />
            </View>
          </View>
          <Text style={styles.count}>{r.votes}</Text>
        </Pressable>
      ))}

      <View style={styles.actions}>
        {winner ? (
          <Button
            label={t('{p0} için yol tarifi', { p0: winner.name })}
            icon="navigate"
            onPress={() => void openDirections(winner.latitude, winner.longitude)}
            accessibilityLabel={t('Önde olan {p0} için yol tarifi', { p0: winner.name })}
          />
        ) : null}
        <Button label={t('Bağlantıyı paylaş')} variant="secondary" icon="share-outline" onPress={() => void share()} />
        <Button label={t('Mekanlara dön')} variant="ghost" onPress={() => router.back()} />
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 20, gap: 4 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 26 },
  meta: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 13, marginTop: 2 },
  hint: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, marginTop: 6, marginBottom: 8 },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    marginTop: 10,
    padding: 10,
    minHeight: 84,
    borderRadius: radii.md,
    backgroundColor: colors.surface,
    borderWidth: 1.5,
    borderColor: colors.line,
    ...shadows.card,
  },
  optionMine: { borderColor: colors.coral },
  thumb: { width: 64, height: 64, borderRadius: radii.sm, overflow: 'hidden', backgroundColor: colors.line },
  optionHead: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  optionName: { flexShrink: 1, color: colors.ink, fontFamily: fonts.sansBold, fontSize: 16 },
  optionSub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12, marginTop: 1 },
  track: { height: 6, borderRadius: 3, backgroundColor: colors.line, marginTop: 8, overflow: 'hidden' },
  fill: { height: 6, borderRadius: 3, backgroundColor: colors.sage },
  fillMine: { backgroundColor: colors.coral },
  count: { color: colors.ink, fontFamily: fonts.display, fontSize: 24, minWidth: 30, textAlign: 'right' },
  actions: { gap: 10, marginTop: 20 },
});
