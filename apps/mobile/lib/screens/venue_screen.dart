import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:nereye/api.dart';
import 'package:nereye/models.dart';
import 'package:url_launcher/url_launcher.dart';

class VenueScreen extends ConsumerStatefulWidget {
  const VenueScreen({super.key, required this.placeId});

  final String placeId;

  @override
  ConsumerState<VenueScreen> createState() => _VenueScreenState();
}

class _VenueScreenState extends ConsumerState<VenueScreen> {
  VenueDetail? _detail;
  List<ReviewItem> _reviews = [];
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final api = ref.read(apiProvider);
      final d = await api.venue(widget.placeId);
      final r = await api.reviews(widget.placeId);
      setState(() {
        _detail = d;
        _reviews = r;
      });
    } catch (e) {
      setState(() => _error = '$e');
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = _detail;
    return Scaffold(
      appBar: AppBar(title: Text(d?.name ?? 'Mekan')),
      body: d == null
          ? Center(child: _error == null ? const CircularProgressIndicator() : Text(_error!))
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Text(d.name, style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800)),
                Text(d.address ?? ''),
                const SizedBox(height: 8),
                Wrap(spacing: 8, children: [
                  if (d.openNow == true) const Chip(label: Text('Açık')),
                  if (d.openNow == false) const Chip(label: Text('Kapalı')),
                  Chip(label: Text(d.nereyeRating == null ? 'Nereye: yeni' : 'Nereye ${d.nereyeRating!.toStringAsFixed(1)}')),
                  if (d.googleRating != null) Chip(label: Text('Google ${d.googleRating!.toStringAsFixed(1)}')),
                  for (final v in d.vibes) Chip(label: Text(v)),
                ]),
                if (d.ownerDescription != null) Padding(padding: const EdgeInsets.only(top: 12), child: Text(d.ownerDescription!)),
                const SizedBox(height: 12),
                if (d.events.isNotEmpty) ...[
                  const Text('Etkinlikler', style: TextStyle(fontWeight: FontWeight.w700)),
                  for (final e in d.events)
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      title: Text(e.title),
                      subtitle: Text(e.startsAt.toLocal().toString()),
                    ),
                ],
                FilledButton.icon(
                  onPressed: () async {
                    final uri = Uri.parse(d.mapsUri ?? 'https://www.google.com/maps/search/?api=1&query=${d.latitude},${d.longitude}');
                    await launchUrl(uri, mode: LaunchMode.externalApplication);
                  },
                  icon: const Icon(Icons.directions),
                  label: const Text('Yol tarifi'),
                ),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: () => ref.read(apiProvider).favorite(widget.placeId),
                  icon: const Icon(Icons.bookmark_add_outlined),
                  label: const Text('Kaydet'),
                ),
                OutlinedButton(
                  onPressed: () => context.push('/venue/${widget.placeId}/review'),
                  child: const Text('Yorum yaz'),
                ),
                OutlinedButton(
                  onPressed: () => context.push('/venue/${widget.placeId}/owner'),
                  child: const Text('İşletme / öne çıkar'),
                ),
                const SizedBox(height: 16),
                const Text('Yorumlar', style: TextStyle(fontWeight: FontWeight.w700)),
                for (final r in _reviews)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text('${r.stars}/5  ${r.text ?? ''}'),
                    subtitle: r.ownerReply == null ? null : Text('İşletme: ${r.ownerReply}'),
                  ),
                const SizedBox(height: 24),
                Text(d.attribution, style: Theme.of(context).textTheme.bodySmall),
              ],
            ),
    );
  }
}
