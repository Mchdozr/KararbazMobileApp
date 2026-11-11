import { dishKind, fallbackPool, pickIndex } from '@/lib/dish-kind';


const pools: Record<string, number[]> = {
  burger: [require('../assets/dishes/burger.jpg')],
  pizza: [require('../assets/dishes/pizza.jpg')],
  doner: [require('../assets/dishes/doner.jpg')],
  kebab: [require('../assets/dishes/kebab.jpg')],
  sushi: [require('../assets/dishes/sushi.jpg')],
  noodles: [require('../assets/dishes/noodles.jpg')],
  chinese: [require('../assets/dishes/chinese.jpg')],
  steak: [require('../assets/dishes/steak.jpg')],
  chicken: [require('../assets/dishes/chicken.jpg')],
  seafood: [require('../assets/dishes/seafood.jpg')],
  pasta: [require('../assets/dishes/pasta.jpg')],
  breakfast: [require('../assets/dishes/breakfast.jpg')],
  dessert: [require('../assets/dishes/dessert.jpg')],
  coffee: [
    require('../assets/dishes/coffee.jpg'),
    require('../assets/dishes/coffee2.jpg'),
    require('../assets/venues/petra.jpg'),
    require('../assets/venues/kronotrop.jpg'),
  ],
  pub: [require('../assets/dishes/pub.jpg'), require('../assets/dishes/pub2.jpg')],
  lounge: [require('../assets/dishes/lounge.jpg'), require('../assets/dishes/lounge2.jpg')],
  park: [require('../assets/dishes/park.jpg'), require('../assets/venues/moda.jpg')],
  museum: [
    require('../assets/dishes/museum.jpg'),
    require('../assets/venues/sureyya.jpg'),
    require('../assets/venues/modern.jpg'),
  ],
  food: [
    require('../assets/dishes/burger.jpg'),
    require('../assets/dishes/pizza.jpg'),
    require('../assets/dishes/doner.jpg'),
    require('../assets/dishes/kebab.jpg'),
    require('../assets/dishes/sushi.jpg'),
    require('../assets/dishes/noodles.jpg'),
    require('../assets/dishes/pasta.jpg'),
    require('../assets/dishes/steak.jpg'),
    require('../assets/dishes/chicken.jpg'),
    require('../assets/dishes/seafood.jpg'),
  ],
  night: [
    require('../assets/dishes/pub.jpg'),
    require('../assets/dishes/pub2.jpg'),
    require('../assets/dishes/lounge.jpg'),
    require('../assets/dishes/lounge2.jpg'),
    require('../assets/venues/opera.jpg'),
    require('../assets/venues/mini.jpg'),
  ],
};

export function fallbackPhoto(placeId: string, name?: string | null, cuisine?: string | null, intent?: string) {
  const nameOfPool = fallbackPool(dishKind(name, cuisine), intent);
  const pool = pools[nameOfPool] ?? pools.food;
  return pool[pickIndex(placeId || name || 'ny', pool.length)];
}
