import { EmptyState } from '@/components/EmptyState';
import { colors } from '@/lib/theme';
import { useRouter } from 'expo-router';
import { StyleSheet, View } from 'react-native';

export default function NotFoundScreen() {
  const router = useRouter();
  return (
    <View style={styles.container}>
      <EmptyState
        icon="map-outline"
        title="Bu sayfa yok"
        body="Bağlantı eski olabilir ya da mekan kaldırılmış olabilir."
        action="Ana sayfaya dön"
        onAction={() => router.replace('/(tabs)')}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', backgroundColor: colors.bg, paddingBottom: 80 },
});
