// Native harita. Web'de aynı ad `Map.web.tsx` dosyasına çözülür (react-native-maps web'de çalışmaz).
import MapView from 'react-native-maps';

export { Marker, Polyline, UrlTile } from 'react-native-maps';
export type { Region } from 'react-native-maps';
export default MapView;
