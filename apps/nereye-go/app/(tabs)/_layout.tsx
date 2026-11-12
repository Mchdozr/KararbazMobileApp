import { haptic } from '@/lib/haptics';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { Tabs } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

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
          borderTopColor: colors.line,
          height: 56 + pad,
          paddingBottom: pad,
          paddingTop: 6,
        },
        tabBarLabelStyle: { fontFamily: fonts.sansBold, fontSize: 11 },
        tabBarHideOnKeyboard: true,
      }}>
      <Tabs.Screen
        name="index"
        options={{
          title: 'Keşfet',
          tabBarAccessibilityLabel: 'Keşfet',
          tabBarIcon: ({ size, focused }) => (
            <Ionicons name={focused ? 'compass' : 'compass-outline'} color={focused ? colors.coral : colors.muted} size={size} />
          ),
        }}
      />
      <Tabs.Screen
        name="map"
        options={{
          title: 'Harita',
          tabBarAccessibilityLabel: 'Harita',
          tabBarIcon: ({ size, focused }) => (
            <Ionicons name={focused ? 'map' : 'map-outline'} color={focused ? colors.coral : colors.muted} size={size} />
          ),
        }}
      />
      <Tabs.Screen
        name="search"
        options={{
          title: 'Ara',
          tabBarAccessibilityLabel: 'Ara',
          tabBarIcon: ({ size, focused }) => (
            <Ionicons name={focused ? 'search' : 'search-outline'} color={focused ? colors.coral : colors.muted} size={size} />
          ),
        }}
      />
      <Tabs.Screen
        name="discover"
        options={{
          title: 'Liste',
          tabBarAccessibilityLabel: 'Liste',
          tabBarIcon: ({ size, focused }) => (
            <Ionicons name={focused ? 'list' : 'list-outline'} color={focused ? colors.coral : colors.muted} size={size} />
          ),
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: 'Profil',
          tabBarAccessibilityLabel: 'Profil',
          tabBarIcon: ({ size, focused }) => (
            <Ionicons name={focused ? 'person' : 'person-outline'} color={focused ? colors.coral : colors.muted} size={size} />
          ),
        }}
      />
    </Tabs>
  );
}
