import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:nereye/models.dart';

const kadikoyLat = 40.9885;
const kadikoyLng = 29.0250;

String defaultApiBase() {
  const fromEnv = String.fromEnvironment('NEREYE_API');
  if (fromEnv.isNotEmpty) {
    return fromEnv;
  }
  if (kIsWeb) {
    return 'http://127.0.0.1:5079';
  }
  if (defaultTargetPlatform == TargetPlatform.android) {
    return 'http://10.0.2.2:5079';
  }
  return 'http://127.0.0.1:5079';
}

final storageProvider = Provider((ref) => const FlutterSecureStorage());

final sessionProvider = StateNotifierProvider<SessionController, AuthTokens?>((ref) {
  return SessionController(ref);
});

class SessionController extends StateNotifier<AuthTokens?> {
  SessionController(this._ref) : super(null);

  final Ref _ref;

  Future<void> restore() async {
    final storage = _ref.read(storageProvider);
    final token = await storage.read(key: 'access');
    if (token == null) {
      return;
    }
    state = AuthTokens(
      accessToken: token,
      refreshToken: await storage.read(key: 'refresh') ?? '',
      userId: await storage.read(key: 'userId') ?? '',
      displayName: await storage.read(key: 'name') ?? '',
      roles: (await storage.read(key: 'roles') ?? '').split(',').where((e) => e.isNotEmpty).toList(),
    );
  }

  Future<void> save(AuthTokens tokens) async {
    final storage = _ref.read(storageProvider);
    await storage.write(key: 'access', value: tokens.accessToken);
    await storage.write(key: 'refresh', value: tokens.refreshToken);
    await storage.write(key: 'userId', value: tokens.userId);
    await storage.write(key: 'name', value: tokens.displayName);
    await storage.write(key: 'roles', value: tokens.roles.join(','));
    state = tokens;
  }

  Future<void> clear() async {
    await _ref.read(storageProvider).deleteAll();
    state = null;
  }
}

final dioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      baseUrl: defaultApiBase(),
      connectTimeout: const Duration(seconds: 15),
    ),
  );
  dio.interceptors.add(
    InterceptorsWrapper(
      onRequest: (options, handler) {
        final session = ref.read(sessionProvider);
        if (session != null) {
          options.headers['Authorization'] = 'Bearer ${session.accessToken}';
        }
        handler.next(options);
      },
    ),
  );
  return dio;
});

final apiProvider = Provider((ref) => NereyeApi(ref.read(dioProvider)));

class NereyeApi {
  NereyeApi(this._dio);
  final Dio _dio;

  Future<AuthTokens> register(String email, String password, String name) async {
    final res = await _dio.post('/api/auth/register', data: {
      'email': email,
      'password': password,
      'displayName': name,
    });
    return AuthTokens.fromJson(res.data as Map<String, dynamic>);
  }

  Future<AuthTokens> login(String email, String password) async {
    final res = await _dio.post('/api/auth/login', data: {
      'email': email,
      'password': password,
    });
    return AuthTokens.fromJson(res.data as Map<String, dynamic>);
  }

  Future<List<VenueCard>> recommend({
    required double lat,
    required double lng,
    required String intent,
    int radius = 1500,
    bool openNow = false,
    List<String> vibes = const [],
  }) async {
    final res = await _dio.get('/api/recommendations', queryParameters: {
      'lat': lat,
      'lng': lng,
      'intent': intent,
      'radiusMeters': radius,
      'openNow': openNow,
      if (vibes.isNotEmpty) 'vibes': vibes.join(','),
    });
    final items = (res.data['items'] as List<dynamic>);
    return items.map((e) => VenueCard.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<VenueDetail> venue(String placeId) async {
    final res = await _dio.get('/api/venues/$placeId');
    return VenueDetail.fromJson(res.data as Map<String, dynamic>);
  }

  Future<List<ReviewItem>> reviews(String placeId) async {
    final res = await _dio.get('/api/venues/$placeId/reviews');
    return (res.data as List<dynamic>).map((e) => ReviewItem.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> writeReview(String placeId, int stars, String text, List<String> tags) async {
    await _dio.post('/api/venues/$placeId/reviews', data: {
      'stars': stars,
      'text': text,
      'tags': tags,
      'photoUrls': <String>[],
    });
  }

  Future<void> favorite(String placeId) => _dio.post('/api/venues/$placeId/favorite');

  Future<List<VenueCard>> search(String q, double lat, double lng) async {
    final res = await _dio.get('/api/search', queryParameters: {'q': q, 'lat': lat, 'lng': lng});
    return (res.data as List<dynamic>).map((e) => VenueCard.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Preferences> preferences() async {
    final res = await _dio.get('/api/me/preferences');
    return Preferences.fromJson(res.data as Map<String, dynamic>);
  }

  Future<Preferences> savePreferences(Preferences p) async {
    final res = await _dio.put('/api/me/preferences', data: {
      'defaultRadiusMeters': p.defaultRadiusMeters,
      'intents': p.intents,
      'vibes': p.vibes,
    });
    return Preferences.fromJson(res.data as Map<String, dynamic>);
  }

  Future<void> claim(String placeId, String message) async {
    await _dio.post('/api/venues/$placeId/claims', data: {'message': message});
  }

  Future<Map<String, dynamic>> createPromotion({
    required String placeId,
    required String intent,
    required String geohash,
    required int days,
    required double dailyBudget,
  }) async {
    final res = await _dio.post('/api/promotions', data: {
      'placeId': placeId,
      'targetIntent': intent,
      'targetGeohash': geohash,
      'days': days,
      'dailyBudget': dailyBudget,
    });
    return res.data as Map<String, dynamic>;
  }

  Future<Map<String, dynamic>> checkout(String campaignId) async {
    final res = await _dio.post('/api/promotions/$campaignId/checkout');
    return res.data as Map<String, dynamic>;
  }

  Future<void> deleteMe() => _dio.delete('/api/auth/me');
}
