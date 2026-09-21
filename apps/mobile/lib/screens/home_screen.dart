import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:nereye/api.dart';
import 'package:nereye/intents.dart';
import 'package:nereye/models.dart';
import 'package:nereye/router.dart';
import 'package:nereye/widgets/intent_chips.dart';
import 'package:nereye/widgets/venue_card_tile.dart';

final recoProvider = FutureProvider<List<VenueCard>>((ref) async {
  final api = ref.watch(apiProvider);
  final intent = ref.watch(selectedIntentProvider);
  final openNow = ref.watch(openNowProvider);
  final vibes = ref.watch(selectedVibesProvider);
  final loc = ref.watch(locationProvider);
  return api.recommend(
    lat: loc.$1,
    lng: loc.$2,
    intent: intent,
    openNow: openNow,
    vibes: vibes,
  );
});

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final intent = ref.watch(selectedIntentProvider);
    final openNow = ref.watch(openNowProvider);
    final reco = ref.watch(recoProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Nereye')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Şimdi ${intentLabel(intent)}', style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800)),
          const SizedBox(height: 12),
          IntentChips(
            value: intent,
            onChanged: (v) => ref.read(selectedIntentProvider.notifier).state = v,
          ),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Şu an açık'),
            value: openNow,
            onChanged: (v) => ref.read(openNowProvider.notifier).state = v,
          ),
          Wrap(
            spacing: 6,
            children: [
              for (final vibe in vibes)
                FilterChip(
                  label: Text(vibe),
                  selected: ref.watch(selectedVibesProvider).contains(vibe),
                  onSelected: (sel) {
                    final current = [...ref.read(selectedVibesProvider)];
                    sel ? current.add(vibe) : current.remove(vibe);
                    ref.read(selectedVibesProvider.notifier).state = current;
                  },
                ),
            ],
          ),
          const SizedBox(height: 12),
          reco.when(
            data: (items) => Column(children: [for (final c in items) VenueCardTile(card: c)]),
            loading: () => const Padding(padding: EdgeInsets.all(32), child: Center(child: CircularProgressIndicator())),
            error: (e, _) => Text('Öneri alınamadı: $e'),
          ),
        ],
      ),
    );
  }
}
