import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:nereye/api.dart';

class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  @override
  void initState() {
    super.initState();
    Future.microtask(() async {
      try {
        await ref.read(sessionProvider.notifier).restore();
      } catch (_) {
        // Test / platform without secure storage.
      }
      if (!mounted) {
        return;
      }
      if (ref.read(sessionProvider) == null) {
        context.go('/auth');
      } else {
        context.go('/onboarding');
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(
        child: Text('Nereye', style: TextStyle(fontSize: 36, fontWeight: FontWeight.w700)),
      ),
    );
  }
}
