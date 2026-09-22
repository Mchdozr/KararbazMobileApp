export type GeoHit = {
  label: string;
  latitude: number;
  longitude: number;
  kind?: string;
  subtitle?: string;
  kindLabel?: string;
  chipLabel?: string;
};

export function geoKindLabel(kind?: string, kindLabel?: string) {
  if (kindLabel?.trim()) {
    return kindLabel.trim();
  }
  switch ((kind ?? '').toLowerCase()) {
    case 'neighbourhood':
    case 'quarter':
    case 'suburb':
    case 'hamlet':
      return 'Mahalle';
    case 'city_district':
    case 'district':
    case 'administrative':
    case 'town':
    case 'municipality':
      return 'İlçe';
    case 'city':
    case 'village':
      return 'Şehir';
    case 'bus_stop':
    case 'station':
    case 'halt':
    case 'tram_stop':
    case 'stop_position':
    case 'stop_area':
      return 'Durak';
    case 'road':
    case 'pedestrian':
    case 'residential':
      return 'Cadde';
    default:
      return 'Konum';
  }
}

export function geoSubtitle(hit: GeoHit) {
  if (hit.subtitle?.trim()) {
    return hit.subtitle.trim();
  }
  return geoKindLabel(hit.kind, hit.kindLabel);
}

export function geoChip(hit: GeoHit) {
  const sub = geoSubtitle(hit);
  if (hit.chipLabel?.trim()) {
    return hit.chipLabel.trim();
  }
  if (sub && sub !== hit.label && sub !== geoKindLabel(hit.kind, hit.kindLabel)) {
    return `${hit.label}, ${sub.split(' · ')[0]}`;
  }
  return hit.label;
}

export function geoDistanceMeters(fromLat: number, fromLng: number, toLat: number, toLng: number) {
  const r = 6371000;
  const p1 = (fromLat * Math.PI) / 180;
  const p2 = (toLat * Math.PI) / 180;
  const dLat = ((toLat - fromLat) * Math.PI) / 180;
  const dLng = ((toLng - fromLng) * Math.PI) / 180;
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(p1) * Math.cos(p2) * Math.sin(dLng / 2) ** 2;
  return Math.round(2 * r * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a)));
}

export function geoKindIcon(kind?: string, kindLabel?: string): 'bus-outline' | 'trail-sign-outline' | 'business-outline' | 'home-outline' | 'location-outline' {
  const k = `${kind ?? ''} ${kindLabel ?? ''}`.toLowerCase();
  if (k.includes('durak') || k.includes('station') || k.includes('bus')) {
    return 'bus-outline';
  }
  if (k.includes('cadde') || k.includes('road')) {
    return 'trail-sign-outline';
  }
  if (k.includes('ilçe') || k.includes('district') || k.includes('şehir') || k.includes('city')) {
    return 'business-outline';
  }
  if (k.includes('mahalle') || k.includes('suburb') || k.includes('neighbour')) {
    return 'home-outline';
  }
  return 'location-outline';
}

export function mahalleLine(address?: string | null) {
  if (!address) {
    return '';
  }
  return address.split(',')[0]?.trim() ?? '';
}
