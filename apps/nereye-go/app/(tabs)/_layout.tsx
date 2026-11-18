import { haptic } from '@/lib/haptics';
import { colors, fonts, shadows } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { Tabs } from 'expo-router';
import type { ComponentProps } from 'react';
import { StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

type IconName = ComponentProps<typeof Ionicons>['name'];

/** Seçili sekmede ikon yumuşak yeşil bir hap üstünde durur. */
function TabIcon({ on, off, focused }: { on: IconName; off: IconName; focused: boolean }) {
  return (
    <View style={[styles.pill, focused && styles.pillOn]}>
      <Ionicons name={focused ? on : off} color={focused ? colors.coral : colors.muted} size={22} />
    </View>
  );
}

export default function TabLayout() {
  const insets = useSafeAreaInsets();
  // Alt güvenli alan (ana ekran çubuğu / hareket çubuğu) etiketlerin üstüne binmesin.
  const pad = Math.max(insets.bottom, 8);
  return (
    <Tabs
      screenListeners={{ tabPress: () => haptic.tap() }}
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: colors.coral,
        tabBarInactiveTintColor: colors.muted,
        tabBarStyle: {
          backgroundColor: colors.surface,
          borderTopWidth: 0,
          borderTopLeftRadius: 24,
          borderTopRightRadius: 24,
          height: 64 + pad,
          paddingBottom: pad,
          paddingTop: 8,
          ...shadows.sheet,
        },
        tabBarLabelStyle: { fontFamily: fonts.sansBold, fontSize: 11, marginTop: 2 },
        tabBarHideOnKeyboard: true,
      }}>
      <Tabs.Screen
        name="index"
        options={{
          title: 'Keşfet',
          tabBarAccessibilityLabel: 'Keşfet',
          tabBarIcon: ({ focused }) => <TabIcon on="compass" off="compass-outline" focused={focused} />,
        }}
      />
      <Tabs.Screen
        name="map"
        options={{
          title: 'Harita',
          tabBarAccessibilityLabel: 'Harita',
          tabBarIcon: ({ focused }) => <TabIcon on="map" off="map-outline" focused={focused} />,
        }}
      />
      <Tabs.Screen
        name="search"
        options={{
          title: 'Ara',
          tabBarAccessibilityLabel: 'Ara',
          tabBarIcon: ({ focused }) => <TabIcon on="search" off="search-outline" focused={focused} />,
        }}
      />
      <Tabs.Screen
        name="discover"
        options={{
          title: 'Liste',
          tabBarAccessibilityLabel: 'Liste',
          tabBarIcon: ({ focused }) => <TabIcon on="list" off="list-outline" focused={focused} />,
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: 'Profil',
          tabBarAccessibilityLabel: 'Profil',
          tabBarIcon: ({ focused }) => <TabIcon on="person" off="person-outline" focused={focused} />,
        }}
      />
    </Tabs>
  );
}

const styles = StyleSheet.create({
  pill: { width: 52, height: 30, borderRadius: 15, alignItems: 'center', justifyContent: 'center' },
  pillOn: { backgroundColor: colors.coralSoft },
});
