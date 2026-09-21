import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:nereye/router.dart';
import 'package:nereye/widgets/intent_chips.dart';

class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key});

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  String _intent = 'Kahve';
  String? _locNote;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Bugün ne?', style: Theme.of(context).textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w800)),
              const SizedBox(height: 8),
              const Text('Niyetini seç, yakındakileri keselim.'),
              const SizedBox(height: 24),
              IntentChips(value: _intent, onChanged: (v) => setState(() => _intent = v)),
              const Spacer(),
              if (_locNote != null) Text(_locNote!),
              FilledButton(
                onPressed: _continue,
                child: const SizedBox(width: double.infinity, child: Center(child: Text('Öner'))),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _continue() async {
    ref.read(selectedIntentProvider.notifier).state = _intent;
    try {
      var perm = await Geolocator.checkPermission();
      if (perm == LocationPermission.denied) {
        perm = await Geolocator.requestPermission();
      }
      if (perm == LocationPermission.denied || perm == LocationPermission.deniedForever) {
        setState(() => _locNote = 'Konum yok; Kadıköy merkezi kullanılıyor.');
      } else {
        final pos = await Geolocator.getCurrentPosition();
        ref.read(locationProvider.notifier).state = (pos.latitude, pos.longitude);
      }
    } catch (_) {
      setState(() => _locNote = 'Konum alınamadı; Kadıköy varsayılan.');
    }
    if (mounted) {
      context.go('/home');
    }
  }
}
