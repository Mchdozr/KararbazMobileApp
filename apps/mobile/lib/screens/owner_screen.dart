import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:nereye/api.dart';
import 'package:nereye/intents.dart';
import 'package:nereye/router.dart';

class OwnerScreen extends ConsumerStatefulWidget {
  const OwnerScreen({super.key, required this.placeId});

  final String placeId;

  @override
  ConsumerState<OwnerScreen> createState() => _OwnerScreenState();
}

class _OwnerScreenState extends ConsumerState<OwnerScreen> {
  final _message = TextEditingController(text: 'İşletmenin sahibiyim.');
  String _intent = 'Kahve';
  int _days = 7;
  double _budget = 150;
  String? _status;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('İşletme')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const Text('1. Sahiplenme talebi (admin onaylar)'),
          TextField(controller: _message, decoration: const InputDecoration(labelText: 'Mesaj')),
          FilledButton(
            onPressed: () async {
              await ref.read(apiProvider).claim(widget.placeId, _message.text);
              setState(() => _status = 'Talep gönderildi. Admin onayından sonra yeniden giriş yap.');
            },
            child: const Text('Sahiplen'),
          ),
          const Divider(height: 32),
          const Text('2. Öne çıkarma (etiketli slot, skor satın alınmaz)'),
          IntentChipsInline(value: _intent, onChanged: (v) => setState(() => _intent = v)),
          Slider(
            value: _days.toDouble(),
            min: 1,
            max: 30,
            divisions: 29,
            label: '$_days gün',
            onChanged: (v) => setState(() => _days = v.round()),
          ),
          Slider(
            value: _budget,
            min: 50,
            max: 500,
            divisions: 9,
            label: '${_budget.round()} ₺/gün',
            onChanged: (v) => setState(() => _budget = v),
          ),
          FilledButton(
            onPressed: () async {
              final loc = ref.read(locationProvider);
              final campaign = await ref.read(apiProvider).createPromotion(
                    placeId: widget.placeId,
                    intent: _intent,
                    geohash: 'sxk967',
                    days: _days,
                    dailyBudget: _budget,
                  );
              final paid = await ref.read(apiProvider).checkout(campaign['id'] as String);
              setState(() => _status = 'Kampanya ${paid['status']} — $loc');
            },
            child: const Text('Sandbox öde ve yayınla'),
          ),
          if (_status != null) Padding(padding: const EdgeInsets.only(top: 16), child: Text(_status!)),
        ],
      ),
    );
  }
}

class IntentChipsInline extends StatelessWidget {
  const IntentChipsInline({super.key, required this.value, required this.onChanged});
  final String value;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 6,
      children: [
        for (final i in intents)
          ChoiceChip(label: Text(intentLabel(i)), selected: value == i, onSelected: (_) => onChanged(i)),
      ],
    );
  }
}
