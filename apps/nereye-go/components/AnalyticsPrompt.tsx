import { t } from '@/lib/i18n';
import { Button } from '@/components/Button';
import { setAnalyticsConsent, useAnalyticsConsent } from '@/lib/analytics';
import { shouldAskConsent } from '@/lib/analytics-queue';
import { useSession } from '@/lib/session';
import { colors, fonts, radii } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';

/**
 * Kullanım istatistiği için tek seferlik, açık rıza kartı. Varsayılan kapalıdır; kullanıcı seçim yapana dek
 * hiçbir olay kaydedilmez. Seçim Profil ekranından değiştirilebilir.
 */
export function AnalyticsPrompt({ sawList }: { sawList: boolean }) {
  const consent = useAnalyticsConsent();
  const { session } = useSession();
  if (consent === 'loading' || !shouldAskConsent(consent === 'unset' ? null : consent, sawList)) {
    return null;
  }
  return (
    <View style={styles.card} accessibilityRole="summary">
      <View style={styles.head}>
        <Ionicons name="stats-chart-outline" size={18} color={colors.coral} />
        <Text style={styles.title}>{t('Nereye\'yi geliştirmemize yardım eder misin?')}</Text>
      </View>
      <Text style={styles.body}>
        {t("Hangi ekranların açıldığını anonim olarak sayarız. Kimliğin, cihazın ve konumun gönderilmez. Kararını Profil'den istediğin an değiştirebilirsin.")}
      </Text>
      <View style={styles.actions}>
        <Button
          label={t('İzin ver')}
          compact
          onPress={() => void setAnalyticsConsent(true, Boolean(session))}
          style={{ flex: 1 }}
          accessibilityLabel={t('Anonim kullanım istatistiğine izin ver')}
        />
        <Button
          label={t('Hayır, teşekkürler')}
          variant="secondary"
          compact
          onPress={() => void setAnalyticsConsent(false, Boolean(session))}
          style={{ flex: 1 }}
          accessibilityLabel={t('Kullanım istatistiğine izin verme')}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    marginHorizontal: 20,
    marginTop: 16,
    padding: 16,
    borderRadius: radii.md,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    gap: 8,
  },
  head: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  title: { flex: 1, color: colors.ink, fontFamily: fonts.sansBold, fontSize: 15 },
  body: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13, lineHeight: 19 },
  actions: { flexDirection: 'row', gap: 10, marginTop: 4 },
});
