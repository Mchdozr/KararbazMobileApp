import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:nereye/router.dart';
import 'package:nereye/screens/home_screen.dart';
import 'package:nereye/widgets/venue_card_tile.dart';

class DiscoverScreen extends ConsumerStatefulWidget {
  const DiscoverScreen({super.key});

  @override
  ConsumerState<DiscoverScreen> createState() => _DiscoverScreenState();
}

class _DiscoverScreenState extends ConsumerState<DiscoverScreen> {
  var _map = false;

  @override
  Widget build(BuildContext context) {
    final reco = ref.watch(recoProvider);
    final loc = ref.watch(locationProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Keşif'),
        actions: [
          IconButton(
            icon: Icon(_map ? Icons.view_list : Icons.map_outlined),
            onPressed: () => setState(() => _map = !_map),
          ),
        ],
      ),
      body: reco.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (items) {
          if (!_map || kIsWeb) {
            return ListView(children: [for (final c in items) VenueCardTile(card: c)]);
          }
          return GoogleMap(
            initialCameraPosition: CameraPosition(target: LatLng(loc.$1, loc.$2), zoom: 14),
            markers: {
              for (final c in items)
                Marker(
                  markerId: MarkerId(c.placeId),
                  position: LatLng(c.latitude, c.longitude),
                  infoWindow: InfoWindow(title: c.name, snippet: c.isSponsored ? 'Öne çıkan' : c.address),
                ),
            },
          );
        },
      ),
    );
  }
}
