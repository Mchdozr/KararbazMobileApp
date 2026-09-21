import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:nereye/api.dart';
import 'package:nereye/intents.dart';
import 'package:nereye/models.dart';
import 'package:nereye/router.dart';

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  Preferences? _prefs;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final p = await ref.read(apiProvider).preferences();
      setState(() => _prefs = p);
    } catch (e) {
      setState(() => _error = '$e');
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(sessionProvider);
    final prefs = _prefs;
    return Scaffold(
      appBar: AppBar(title: const Text('Profil')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(session?.displayName ?? '', style: Theme.of(context).textTheme.headlineSmall),
          Text(session?.roles.join(', ') ?? ''),
          const SizedBox(height: 16),
          if (_error != null) Text(_error!),
          if (prefs != null) ...[
            Text('Yarıçap: ${prefs.defaultRadiusMeters} m'),
            Slider(
              value: prefs.defaultRadiusMeters.toDouble().clamp(300, 3000),
              min: 300,
              max: 3000,
              divisions: 9,
              onChanged: (v) => setState(
                () => _prefs = Preferences(
                  defaultRadiusMeters: v.round(),
                  intents: prefs.intents,
                  vibes: prefs.vibes,
                ),
              ),
            ),
            const Text('Niyetler'),
            Wrap(
              spacing: 6,
              children: [
                for (final i in intents)
                  FilterChip(
                    label: Text(intentLabel(i)),
                    selected: prefs.intents.contains(i),
                    onSelected: (s) {
                      final next = [...prefs.intents];
                      s ? next.add(i) : next.remove(i);
                      setState(() => _prefs = Preferences(defaultRadiusMeters: prefs.defaultRadiusMeters, intents: next, vibes: prefs.vibes));
                    },
                  ),
              ],
            ),
            const SizedBox(height: 8),
            const Text('Vibe'),
            Wrap(
              spacing: 6,
              children: [
                for (final v in vibes)
                  FilterChip(
                    label: Text(v),
                    selected: prefs.vibes.contains(v),
                    onSelected: (s) {
                      final next = [...prefs.vibes];
                      s ? next.add(v) : next.remove(v);
                      setState(() => _prefs = Preferences(defaultRadiusMeters: prefs.defaultRadiusMeters, intents: prefs.intents, vibes: next));
                    },
                  ),
              ],
            ),
            FilledButton(
              onPressed: () async {
                final saved = await ref.read(apiProvider).savePreferences(prefs);
                ref.read(selectedVibesProvider.notifier).state = saved.vibes;
                setState(() => _prefs = saved);
              },
              child: const Text('Tercihleri kaydet'),
            ),
          ],
          const SizedBox(height: 24),
          TextButton(
            onPressed: () async {
              await ref.read(sessionProvider.notifier).clear();
              if (context.mounted) {
                context.go('/auth');
              }
            },
            child: const Text('Çıkış'),
          ),
          TextButton(
            onPressed: () async {
              await ref.read(apiProvider).deleteMe();
              await ref.read(sessionProvider.notifier).clear();
              if (context.mounted) {
                context.go('/auth');
              }
            },
            child: const Text('Hesabı sil (KVKK)'),
          ),
        ],
      ),
    );
  }
}
