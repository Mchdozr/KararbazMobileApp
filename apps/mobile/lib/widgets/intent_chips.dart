import 'package:flutter/material.dart';
import 'package:nereye/intents.dart';

class IntentChips extends StatelessWidget {
  const IntentChips({super.key, required this.value, required this.onChanged});

  final String value;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final intent in intents)
          ChoiceChip(
            label: Text(intentLabel(intent)),
            selected: value == intent,
            onSelected: (_) => onChanged(intent),
          ),
      ],
    );
  }
}
