import { venueShareText } from '@/lib/share-text';
import { Share } from 'react-native';

export { venueShareText };

export async function shareVenue(placeId: string, name: string, address: string | null | undefined) {
  try {
    await Share.share({ message: venueShareText(placeId, name, address), title: name });
  } catch {
    /* kullanıcı vazgeçti */
  }
}
