import { Button } from '@/components/Button';
import { Stars } from '@/components/Stars';
import { TextField } from '@/components/TextField';
import { api } from '@/lib/api';
import { intentLabel } from '@/lib/constants';
import { friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { relativeDate } from '@/lib/reviews';
import { useSession } from '@/lib/session';
import { colors, fonts, shadows } from '@/lib/theme';
import { promoContactUrl } from '@/lib/promo-contact';
import { toast } from '@/lib/toast';
import type { PromotionItem, ReviewItem, VenueStats } from '@/lib/types';
import { statBars, statTiles } from '@/lib/venue-stats';
import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { KeyboardAvoidingView, Linking, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

// v1'de uygulama içi tahsilat yok; başvuru adresi derlemeye insan tarafından verilir.
const promoContact = promoContactUrl(process.env.EXPO_PUBLIC_PROMOTION_CONTACT_URL);
const STATUS_TR: Record<string, string> = {
  Draft: 'Taslak',
  AwaitingPayment: 'Ödeme bekliyor',
  Active: 'Yayında',
  Expired: 'Süresi doldu',
  Cancelled: 'İptal',
};

function Section({ title, sub, children }: { title: string; sub?: string; children: React.ReactNode }) {
  return (
    <View style={styles.card}>
      <Text style={styles.h}>{title}</Text>
      {sub ? <Text style={styles.sub}>{sub}</Text> : null}
      <View style={styles.cardBody}>{children}</View>
    </View>
  );
}

export default function Owner() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { session } = useSession();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const owner = Boolean(session?.roles.includes('VenueOwner'));
  const [message, setMessage] = useState('');
  const [claimed, setClaimed] = useState(false);
  const [name, setName] = useState('');
  const [desc, setDesc] = useState('');
  const [reviews, setReviews] = useState<ReviewItem[]>([]);
  const [replyTo, setReplyTo] = useState<string | null>(null);
  const [reply, setReply] = useState('');
  const [mine, setMine] = useState<PromotionItem[]>([]);
  const [busy, setBusy] = useState<string | null>(null);
  const [stats, setStats] = useState<VenueStats | null>(null);

  const load = useCallback(async () => {
    try {
      const v = await api.venue(id!);
      setName(v.name);
      setDesc(v.ownerDescription ?? '');
    } catch {
      /* mekan bilgisi gelmezse form boş kalır */
    }
    if (owner) {
      setReviews((await api.reviews(id!).catch(() => [])) ?? []);
      setMine(await api.myPromotions().catch(() => []));
      setStats(await api.venueStats(id!).catch(() => null));
    }
  }, [id, owner]);

  useEffect(() => {
    if (!session) {
      router.replace('/auth');
      return;
    }
    void load();
  }, [session, router, load]);

  /** İşlemi çalıştırır; düğme meşgul görünür, sonuç toast ile bildirilir. */
  async function run(key: string, work: () => Promise<void>, ok: string) {
    setBusy(key);
    try {
      await work();
      haptic.success();
      toast.success(ok);
    } catch (e) {
      haptic.warn();
      toast.error(friendlyError(e));
    } finally {
      setBusy(null);
    }
  }

  async function uploadPhoto() {
    // Sistem fotoğraf seçicisi izin istemez (Android PickVisualMedia); galeri izni pakette yok.
    const picked = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: 0.7, base64: true });
    const asset = picked.assets?.[0];
    if (picked.canceled || !asset?.base64) {
      return;
    }
    await run('photo', async () => void (await api.uploadOwnerPhoto(id!, asset.base64!)), 'Fotoğraf yüklendi');
  }

  const forThisVenue = mine.filter((p) => p.placeId === id);

  return (
    <KeyboardAvoidingView style={styles.bg} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={64}>
      <ScrollView
        contentContainerStyle={[styles.wrap, { paddingBottom: insets.bottom + 32 }]}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}>
        {!owner ? (
          <Section
            title="Bu mekânı sahiplen"
            sub="İşletmenin sahibi ya da yetkilisiysen talep gönder. Onaylandığında profilini düzenleyebilir, yorumlara yanıt verebilirsin.">
            {claimed ? (
              <View style={styles.done}>
                <Ionicons name="checkmark-circle" size={22} color={colors.sage} />
                <Text style={styles.doneText}>Talebin alındı. Onaylandıktan sonra çıkış yapıp tekrar giriş yap.</Text>
              </View>
            ) : (
              <>
                <TextField
                  label="Kendini tanıt"
                  placeholder="Örn. İşletmenin sahibiyim, vergi levhası gönderebilirim."
                  value={message}
                  onChangeText={setMessage}
                  multiline
                  maxLength={500}
                  counter={500}
                />
                <Button
                  label="Talep gönder"
                  loading={busy === 'claim'}
                  disabled={message.trim().length < 10}
                  onPress={() =>
                    void run(
                      'claim',
                      async () => {
                        await api.claim(id!, message.trim());
                        setClaimed(true);
                      },
                      'Talep gönderildi',
                    )
                  }
                />
              </>
            )}
          </Section>
        ) : (
          <>
            {stats ? (
              <Section
                title="Son 30 gün"
                sub="Görüntülenme ve yol tarifi, kullanım istatistiğine izin veren kullanıcılardan sayılır; kaydetme tüm kullanıcıların toplamıdır.">
                <View style={styles.tiles}>
                  {statTiles(stats).map((t) => (
                    <View key={t.label} style={styles.tile} accessible accessibilityLabel={`${t.label}: ${t.value}${t.note ? `, ${t.note}` : ''}`}>
                      <Text style={styles.tileValue}>{t.value}</Text>
                      <Text style={styles.tileLabel}>{t.label}</Text>
                      {t.note ? <Text style={styles.tileNote}>{t.note}</Text> : null}
                    </View>
                  ))}
                </View>
                <View style={styles.bars} accessibilityLabel="Son 14 günün günlük görüntülenmesi">
                  {statBars(stats.days, 14).map((b) => (
                    <View key={b.label} style={styles.barCol}>
                      <View style={[styles.bar, { height: 4 + b.ratio * 56, opacity: b.value ? 1 : 0.35 }]} />
                    </View>
                  ))}
                </View>
                <Text style={styles.fine}>Son 14 gün, günlük görüntülenme</Text>
              </Section>
            ) : null}
            <Section title="İşletme profili" sub="Mekan sayfasında görünen ad ve açıklama.">
              <TextField label="Görünen ad" value={name} onChangeText={setName} maxLength={80} />
              <TextField
                label="Açıklama"
                placeholder="Misafirlerine kendini anlat."
                value={desc}
                onChangeText={setDesc}
                multiline
                maxLength={600}
                counter={600}
              />
              <Button
                label="Kaydet"
                loading={busy === 'profile'}
                disabled={!name.trim()}
                onPress={() => void run('profile', () => api.updateOwner(id!, name.trim(), desc.trim()).then(() => {}), 'Profil güncellendi')}
              />
              <Button label="Fotoğraf yükle" icon="image-outline" variant="outline" loading={busy === 'photo'} onPress={() => void uploadPhoto()} />
            </Section>

            <Section title="Yorumlar" sub={reviews.length ? 'Yanıtın yorumun altında herkese görünür.' : 'Henüz yorum yok.'}>
              {reviews.map((r) => (
                <View key={r.id} style={styles.review}>
                  <View style={styles.reviewHead}>
                    <Text style={styles.author}>{r.authorName || 'Gezgin'}</Text>
                    <Stars value={r.stars} size={12} />
                    <Text style={styles.when}>{relativeDate(r.createdAt)}</Text>
                  </View>
                  {r.text ? <Text style={styles.reviewText}>{r.text}</Text> : null}
                  {r.ownerReply && replyTo !== r.id ? <Text style={styles.replied}>Yanıtın: {r.ownerReply}</Text> : null}
                  {replyTo === r.id ? (
                    <>
                      <TextField label="Yanıtın" value={reply} onChangeText={setReply} multiline maxLength={1000} counter={1000} autoFocus />
                      <View style={styles.rowBtns}>
                        <Button label="Vazgeç" variant="ghost" compact onPress={() => setReplyTo(null)} />
                        <Button
                          label="Yanıtla"
                          compact
                          style={{ flex: 1 }}
                          loading={busy === `reply-${r.id}`}
                          disabled={!reply.trim()}
                          onPress={() =>
                            void run(
                              `reply-${r.id}`,
                              async () => {
                                await api.replyReview(r.id, reply.trim());
                                setReviews((prev) => prev.map((x) => (x.id === r.id ? { ...x, ownerReply: reply.trim() } : x)));
                                setReplyTo(null);
                              },
                              'Yanıtın yayınlandı',
                            )
                          }
                        />
                      </View>
                    </>
                  ) : (
                    <Button
                      label={r.ownerReply ? 'Yanıtı düzenle' : 'Yanıtla'}
                      icon="chatbubble-outline"
                      variant="outline"
                      compact
                      onPress={() => {
                        setReply(r.ownerReply ?? '');
                        setReplyTo(r.id);
                      }}
                    />
                  )}
                </View>
              ))}
            </Section>

            <Section title="Öne çıkar" sub="Mekanın, yakındaki kullanıcılara “Öne çıkan” etiketiyle gösterilebilir.">
              {forThisVenue.map((p) => (
                <View key={p.id} style={styles.campaign}>
                  <Ionicons name="megaphone-outline" size={18} color={colors.coral} />
                  <View style={{ flex: 1 }}>
                    <Text style={styles.author}>{intentLabel(p.targetIntent)}</Text>
                    <Text style={styles.when}>
                      {new Date(p.startAt).toLocaleDateString('tr-TR')} – {new Date(p.endAt).toLocaleDateString('tr-TR')} · günlük {p.dailyBudget} ₺
                    </Text>
                  </View>
                  <Text style={styles.status}>{STATUS_TR[p.status] ?? p.status}</Text>
                </View>
              ))}
              {forThisVenue.length === 0 ? (
                <Text style={styles.fine}>Bu mekan için açık bir kampanya yok.</Text>
              ) : null}
              <Button
                label="Öne çıkarmak için bize ulaşın"
                icon="mail-outline"
                variant={promoContact ? 'primary' : 'outline'}
                disabled={!promoContact}
                onPress={() => {
                  if (promoContact) {
                    void Linking.openURL(promoContact).catch(() => toast.error('Bağlantı açılamadı.'));
                  }
                }}
              />
              <Text style={styles.fine}>
                {promoContact
                  ? 'Uygulama içinden ödeme alınmıyor; kampanyayı ekibimiz senin için açar.'
                  : 'İletişim bağlantısı henüz yok.'}
              </Text>
            </Section>
          </>
        )}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 16, gap: 14 },
  card: { backgroundColor: colors.surface, borderRadius: 24, padding: 20, ...shadows.card },
  cardBody: { gap: 12, marginTop: 14 },
  h: { fontSize: 22, fontFamily: fonts.display, color: colors.ink },
  sub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20, marginTop: 4 },
  done: { flexDirection: 'row', gap: 10, alignItems: 'center', backgroundColor: colors.coralSoft, borderRadius: 14, padding: 14 },
  doneText: { flex: 1, color: colors.espresso, fontFamily: fonts.sansMed, fontSize: 14, lineHeight: 20 },
  review: { backgroundColor: colors.bg, borderRadius: 16, padding: 14, gap: 10 },
  reviewHead: { flexDirection: 'row', alignItems: 'center', gap: 8, flexWrap: 'wrap' },
  author: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 14 },
  when: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12 },
  reviewText: { color: colors.ink, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20 },
  replied: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13, lineHeight: 19, fontStyle: 'italic' },
  rowBtns: { flexDirection: 'row', gap: 8 },
  campaign: { flexDirection: 'row', alignItems: 'center', gap: 10, backgroundColor: colors.bg, borderRadius: 14, padding: 12 },
  status: { color: colors.sage, fontFamily: fonts.sansBold, fontSize: 12 },
  label: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 13 },
  fine: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12, textAlign: 'center' },
  tiles: { flexDirection: 'row', gap: 10 },
  tile: { flex: 1, backgroundColor: colors.bg, borderRadius: 16, padding: 12 },
  tileValue: { color: colors.ink, fontFamily: fonts.display, fontSize: 26 },
  tileLabel: { color: colors.muted, fontFamily: fonts.sansBold, fontSize: 12 },
  tileNote: { color: colors.coral, fontFamily: fonts.sansMed, fontSize: 11, marginTop: 2 },
  bars: { flexDirection: 'row', alignItems: 'flex-end', gap: 4, height: 64 },
  barCol: { flex: 1, justifyContent: 'flex-end' },
  bar: { borderRadius: 4, backgroundColor: colors.coral },
});
