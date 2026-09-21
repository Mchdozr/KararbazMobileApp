class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.refreshToken,
    required this.userId,
    required this.displayName,
    required this.roles,
  });

  final String accessToken;
  final String refreshToken;
  final String userId;
  final String displayName;
  final List<String> roles;

  bool get isOwner => roles.contains('VenueOwner');

  factory AuthTokens.fromJson(Map<String, dynamic> json) => AuthTokens(
        accessToken: json['accessToken'] as String,
        refreshToken: json['refreshToken'] as String,
        userId: json['userId'] as String,
        displayName: json['displayName'] as String,
        roles: (json['roles'] as List<dynamic>).cast<String>(),
      );
}

class VenueCard {
  const VenueCard({
    required this.placeId,
    required this.name,
    required this.distanceMeters,
    required this.kind,
    this.address,
    this.openNow,
    this.nereyeRating,
    this.nereyeRatingCount = 0,
    this.photoUrl,
    this.vibes = const [],
    this.upcomingEvents = const [],
    this.latitude = 0,
    this.longitude = 0,
  });

  final String placeId;
  final String name;
  final String? address;
  final double distanceMeters;
  final String kind;
  final bool? openNow;
  final double? nereyeRating;
  final int nereyeRatingCount;
  final String? photoUrl;
  final List<String> vibes;
  final List<CultureEvent> upcomingEvents;
  final double latitude;
  final double longitude;

  bool get isSponsored => kind == 'Sponsored';

  factory VenueCard.fromJson(Map<String, dynamic> json) => VenueCard(
        placeId: json['placeId'] as String,
        name: json['name'] as String,
        address: json['address'] as String?,
        distanceMeters: (json['distanceMeters'] as num).toDouble(),
        kind: json['kind'] as String? ?? 'Organic',
        openNow: json['openNow'] as bool?,
        nereyeRating: (json['nereyeRating'] as num?)?.toDouble(),
        nereyeRatingCount: json['nereyeRatingCount'] as int? ?? 0,
        photoUrl: json['photoUrl'] as String?,
        vibes: (json['vibes'] as List<dynamic>? ?? []).map((e) => e.toString()).toList(),
        upcomingEvents: (json['upcomingEvents'] as List<dynamic>? ?? [])
            .map((e) => CultureEvent.fromJson(e as Map<String, dynamic>))
            .toList(),
        latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
        longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
      );
}

class CultureEvent {
  const CultureEvent({required this.id, required this.title, required this.startsAt, this.endsAt});

  final String id;
  final String title;
  final DateTime startsAt;
  final DateTime? endsAt;

  factory CultureEvent.fromJson(Map<String, dynamic> json) => CultureEvent(
        id: json['id'] as String,
        title: json['title'] as String,
        startsAt: DateTime.parse(json['startsAt'] as String),
        endsAt: json['endsAt'] == null ? null : DateTime.parse(json['endsAt'] as String),
      );
}

class VenueDetail {
  const VenueDetail({
    required this.placeId,
    required this.name,
    required this.attribution,
    this.address,
    this.openNow,
    this.googleRating,
    this.nereyeRating,
    this.claimed = false,
    this.mapsUri,
    this.ownerDescription,
    this.vibes = const [],
    this.events = const [],
    this.latitude = 0,
    this.longitude = 0,
  });

  final String placeId;
  final String name;
  final String? address;
  final bool? openNow;
  final double? googleRating;
  final double? nereyeRating;
  final bool claimed;
  final String? mapsUri;
  final String? ownerDescription;
  final String attribution;
  final List<String> vibes;
  final List<CultureEvent> events;
  final double latitude;
  final double longitude;

  factory VenueDetail.fromJson(Map<String, dynamic> json) {
    final google = json['google'] as Map<String, dynamic>;
    final nereye = json['nereye'] as Map<String, dynamic>;
    return VenueDetail(
      placeId: json['placeId'] as String,
      name: (nereye['ownerDisplayName'] as String?) ?? google['name'] as String,
      address: google['address'] as String?,
      openNow: google['openNow'] as bool?,
      googleRating: (google['googleRating'] as num?)?.toDouble(),
      nereyeRating: (nereye['ratingAvg'] as num?)?.toDouble(),
      claimed: nereye['claimed'] as bool? ?? false,
      mapsUri: google['googleMapsUri'] as String?,
      ownerDescription: nereye['ownerDescription'] as String?,
      attribution: google['attribution'] as String? ?? 'Harita verisi: Google',
      vibes: (nereye['vibes'] as List<dynamic>? ?? []).map((e) => e.toString()).toList(),
      events: (json['events'] as List<dynamic>? ?? [])
          .map((e) => CultureEvent.fromJson(e as Map<String, dynamic>))
          .toList(),
      latitude: (google['latitude'] as num?)?.toDouble() ?? 0,
      longitude: (google['longitude'] as num?)?.toDouble() ?? 0,
    );
  }
}

class ReviewItem {
  const ReviewItem({
    required this.id,
    required this.stars,
    this.text,
    this.ownerReply,
  });

  final String id;
  final int stars;
  final String? text;
  final String? ownerReply;

  factory ReviewItem.fromJson(Map<String, dynamic> json) => ReviewItem(
        id: json['id'] as String,
        stars: json['stars'] as int,
        text: json['text'] as String?,
        ownerReply: json['ownerReply'] as String?,
      );
}

class Preferences {
  const Preferences({
    required this.defaultRadiusMeters,
    required this.intents,
    required this.vibes,
  });

  final int defaultRadiusMeters;
  final List<String> intents;
  final List<String> vibes;

  factory Preferences.fromJson(Map<String, dynamic> json) => Preferences(
        defaultRadiusMeters: json['defaultRadiusMeters'] as int,
        intents: (json['intents'] as List<dynamic>).map((e) => e.toString()).toList(),
        vibes: (json['vibes'] as List<dynamic>).map((e) => e.toString()).toList(),
      );
}
