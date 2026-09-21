import { BackButton } from '@/components/BackButton';
import { LocationPicker } from '@/components/LocationPicker';
import { colors, fonts } from '@/lib/theme';
import { useRouter } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export default function PlacesScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();

  return (
    <View style={[styles.bg, { paddingTop: insets.top + 10 }]}>
      <View style={styles.top}>
        {router.canGoBack() ? <BackButton /> : null}
        <Text style={styles.h}>Neredesin?</Text>
      </View>
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
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg, paddingHorizontal: 20 },
  top: { flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 8 },
  h: { fontFamily: fonts.display, fontSize: 32, color: colors.ink, flex: 1 },
});
