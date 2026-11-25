import { t } from '@/lib/i18n';
import { LocationPicker } from '@/components/LocationPicker';
import { ScreenHeader } from '@/components/ScreenHeader';
import { colors } from '@/lib/theme';
import { useRouter } from 'expo-router';
import { StyleSheet, View } from 'react-native';

export default function PlacesScreen() {
  const router = useRouter();

  return (
    <View style={styles.bg}>
      <ScreenHeader back={router.canGoBack()} title={t('Neredesin?')} subtitle={t('Öneriler bu konuma göre sıralanır.')} />
      <View style={styles.body}>
        <LocationPicker
          onPicked={() => {
            if (router.canGoBack()) {
              router.back();
            } else {
              router.replace('/(tabs)');
            }
          }}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  body: { flex: 1, paddingHorizontal: 20, paddingTop: 16 },
});
