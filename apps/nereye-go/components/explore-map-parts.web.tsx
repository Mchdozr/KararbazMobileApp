import { View } from 'react-native';

export const ExploreMapView = View;
export const ExploreMarker = (_props: { coordinate?: unknown; title?: string; pinColor?: string; onPress?: () => void }) => null;
export const ExploreUrlTile = (_props: { urlTemplate?: string; maximumZ?: number }) => null;

export type Region = {
  latitude: number;
  longitude: number;
  latitudeDelta: number;
  longitudeDelta: number;
};
