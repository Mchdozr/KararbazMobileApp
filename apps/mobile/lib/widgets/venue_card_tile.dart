import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:nereye/models.dart';

class VenueCardTile extends StatelessWidget {
  const VenueCardTile({super.key, required this.card});

  final VenueCard card;

  @override
  Widget build(BuildContext context) {
    return Card(
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        onTap: () => context.push('/venue/${card.placeId}'),
        title: Row(
          children: [
            Expanded(child: Text(card.name, style: const TextStyle(fontWeight: FontWeight.w700))),
            if (card.isSponsored)
              const Padding(
                padding: EdgeInsets.only(left: 6),
                child: Chip(label: Text('Öne çıkan'), visualDensity: VisualDensity.compact),
              ),
          ],
        ),
        subtitle: Text(
          [
            '${card.distanceMeters.round()} m',
            if (card.openNow == true) 'Açık',
            if (card.openNow == false) 'Kapalı',
            if (card.nereyeRating != null) 'Nereye ${card.nereyeRating!.toStringAsFixed(1)}',
            if (card.nereyeRating == null) 'Yeni',
            if (card.upcomingEvents.isNotEmpty) card.upcomingEvents.first.title,
          ].join(' · '),
        ),
        trailing: const Icon(Icons.chevron_right),
      ),
    );
  }
}
