import { colors, fonts } from '@/lib/theme';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

const SECTIONS: { title: string; body: string }[] = [
  {
    title: 'Hangi verileri işliyoruz?',
    body:
      'Hesap bilgilerin (ad, e-posta), seçtiğin niyet ve atmosfer tercihleri, kaydettiğin mekanlar ve konumlar, yazdığın yorumlar ile yüklediğin fotoğraflar. Konumunu yalnızca sen izin verdiğinde ve yakınındaki mekanları önermek için kullanırız.',
  },
  {
    title: 'Neden işliyoruz?',
    body:
      'Sana uygun mekan önerileri sunmak, hesabını ve oturumunu yönetmek, yorum ve şikayetleri değerlendirmek, hizmeti güvenli tutmak için.',
  },
  {
    title: 'Konum verisi',
    body:
      'Konumun cihazında tutulur ve öneri isteğiyle birlikte sunucuya yaklaşık koordinat olarak gönderilir. Geçmiş konumlarını saklamayız. İzni cihaz ayarlarından istediğin an kapatabilirsin; uygulama semt seçerek de çalışır.',
  },
  {
    title: 'Kimlerle paylaşıyoruz?',
    body:
      'Harita ve mekan bilgileri için OpenStreetMap tabanlı servislere arama metni ve koordinat iletilir; bu isteklere kimliğin eklenmez. Verilerini reklam amacıyla üçüncü kişilere satmayız.',
  },
  {
    title: 'Hakların',
    body:
      'KVKK madde 11 kapsamında verilerine erişme, düzeltme, silme ve işlemeye itiraz etme hakkın var. Hesabını Profil › Hesap › Hesabı sil adımından kalıcı olarak silebilirsin; silindiğinde kayıtlı mekanların, konumların ve tercihlerin kaldırılır, yorum metinlerin anonimleştirilir.',
  },
  {
    title: 'İletişim',
    body: 'Sorular ve başvurular için: kvkk@nereye.app',
  },
];

export default function Legal() {
  const insets = useSafeAreaInsets();
  return (
    <ScrollView style={styles.bg} contentContainerStyle={[styles.wrap, { paddingBottom: insets.bottom + 32 }]}>
      <Text style={styles.h}>Kişisel verilerin{'\n'}sende kalsın.</Text>
      <Text style={styles.lead}>
        Bu metin, 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında Nereye uygulamasının verilerini nasıl
        işlediğini özetler.
      </Text>
      {SECTIONS.map((s) => (
        <View key={s.title} style={styles.card}>
          <Text style={styles.title}>{s.title}</Text>
          <Text style={styles.body}>{s.body}</Text>
        </View>
      ))}
      <Text style={styles.foot}>Harita verisi © OpenStreetMap katkıcıları.</Text>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 20, gap: 12 },
  h: { fontFamily: fonts.display, fontSize: 30, lineHeight: 36, color: colors.ink },
  lead: { color: colors.muted, fontSize: 15, lineHeight: 22, fontFamily: fonts.sans, marginBottom: 4 },
  card: {
    backgroundColor: colors.surface,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.line,
    padding: 16,
    gap: 6,
  },
  title: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 15 },
  body: { color: colors.ink, opacity: 0.86, fontSize: 14, lineHeight: 21, fontFamily: fonts.sans },
  foot: { color: colors.muted, fontSize: 12, marginTop: 8, textAlign: 'center' },
});
