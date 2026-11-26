import { Image } from 'expo-image';

const LOGO = require('../assets/images/logo.png');

export function BrandLogo({
  size = 44,
}: {
  size?: number;
}) {
  return (
    <Image
      source={LOGO}
      style={{ width: size, height: size, borderRadius: size * 0.12 }}
      contentFit="contain"
      accessibilityLabel="Kararbaz"
    />
  );
}
