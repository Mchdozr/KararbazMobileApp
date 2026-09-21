import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:nereye/api.dart';
import 'package:nereye/screens/auth_screen.dart';
import 'package:nereye/screens/discover_screen.dart';
import 'package:nereye/screens/home_screen.dart';
import 'package:nereye/screens/onboarding_screen.dart';
import 'package:nereye/screens/owner_screen.dart';
import 'package:nereye/screens/profile_screen.dart';
import 'package:nereye/screens/review_screen.dart';
import 'package:nereye/screens/search_screen.dart';
import 'package:nereye/screens/shell_screen.dart';
import 'package:nereye/screens/splash_screen.dart';
import 'package:nereye/screens/venue_screen.dart';

final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    initialLocation: '/splash',
    routes: [
      GoRoute(path: '/splash', builder: (c, s) => const SplashScreen()),
      GoRoute(path: '/auth', builder: (c, s) => const AuthScreen()),
      GoRoute(path: '/onboarding', builder: (c, s) => const OnboardingScreen()),
      StatefulShellRoute.indexedStack(
        builder: (c, s, shell) => ShellScreen(shell: shell),
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(path: '/home', builder: (c, s) => const HomeScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/discover', builder: (c, s) => const DiscoverScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/search', builder: (c, s) => const SearchScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/profile', builder: (c, s) => const ProfileScreen()),
          ]),
        ],
      ),
      GoRoute(
        path: '/venue/:id',
        builder: (c, s) => VenueScreen(placeId: s.pathParameters['id']!),
      ),
      GoRoute(
        path: '/venue/:id/review',
        builder: (c, s) => ReviewScreen(placeId: s.pathParameters['id']!),
      ),
      GoRoute(
        path: '/venue/:id/owner',
        builder: (c, s) => OwnerScreen(placeId: s.pathParameters['id']!),
      ),
    ],
  );
});

final selectedIntentProvider = StateProvider<String>((ref) => 'Kahve');
final openNowProvider = StateProvider<bool>((ref) => false);
final selectedVibesProvider = StateProvider<List<String>>((ref) => []);
final locationProvider = StateProvider<(double, double)>((ref) => (kadikoyLat, kadikoyLng));
