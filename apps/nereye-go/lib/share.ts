import { Share } from 'react-native';

export function venueShareText(name: string, address: string | null | undefined, latitude: number, longitude: number) {
  const where = address ? ` — ${address}` : '';
  return `${name}${where}\nhttps://www.openstreetmap.org/?mlat=${latitude}&mlon=${longitude}#map=18/${latitude}/${longitude}\nNereye ile buldum.`;
}

export async function shareVenue(name: string, address: string | null | undefined, latitude: number, longitude: number) {
  try {
    await Share.share({ message: venueShareText(name, address, latitude, longitude), title: name });
  } catch {
    /* kullanıcı vazgeçti */
  }
}
