import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:nereye/api.dart';
import 'package:nereye/models.dart';
import 'package:nereye/router.dart';
import 'package:nereye/widgets/venue_card_tile.dart';

class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  final _q = TextEditingController();
  List<VenueCard> _items = [];
  String? _error;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Ara')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(16),
            child: TextField(
              controller: _q,
              decoration: const InputDecoration(hintText: 'Mekan veya semt', suffixIcon: Icon(Icons.search)),
              onSubmitted: (_) => _run(),
            ),
          ),
          if (_error != null) Text(_error!),
          Expanded(
            child: ListView(children: [for (final c in _items) VenueCardTile(card: c)]),
          ),
        ],
      ),
    );
  }

  Future<void> _run() async {
    try {
      final loc = ref.read(locationProvider);
      final items = await ref.read(apiProvider).search(_q.text, loc.$1, loc.$2);
      setState(() {
        _items = items;
        _error = null;
      });
    } catch (e) {
      setState(() => _error = '$e');
    }
  }
}
