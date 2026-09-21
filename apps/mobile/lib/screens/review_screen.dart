import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:nereye/api.dart';
import 'package:nereye/intents.dart';

class ReviewScreen extends ConsumerStatefulWidget {
  const ReviewScreen({super.key, required this.placeId});

  final String placeId;

  @override
  ConsumerState<ReviewScreen> createState() => _ReviewScreenState();
}

class _ReviewScreenState extends ConsumerState<ReviewScreen> {
  int _stars = 5;
  final _text = TextEditingController();
  final _tags = <String>{};

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Yorum yaz')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Slider(
            value: _stars.toDouble(),
            min: 1,
            max: 5,
            divisions: 4,
            label: '$_stars',
            onChanged: (v) => setState(() => _stars = v.round()),
          ),
          TextField(controller: _text, decoration: const InputDecoration(labelText: 'Yorum'), maxLines: 4),
          const SizedBox(height: 12),
          Wrap(
            spacing: 6,
            children: [
              for (final v in vibes)
                FilterChip(
                  label: Text(v),
                  selected: _tags.contains(v),
                  onSelected: (s) => setState(() => s ? _tags.add(v) : _tags.remove(v)),
                ),
            ],
          ),
          const SizedBox(height: 24),
          FilledButton(
            onPressed: () async {
              await ref.read(apiProvider).writeReview(widget.placeId, _stars, _text.text, _tags.toList());
              if (context.mounted) {
                Navigator.pop(context);
              }
            },
            child: const Text('Gönder'),
          ),
        ],
      ),
    );
  }
}
