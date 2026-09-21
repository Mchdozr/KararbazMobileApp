import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:nereye/router.dart';
import 'package:nereye/theme.dart';

class NereyeApp extends ConsumerWidget {
  const NereyeApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    return MaterialApp.router(
      title: 'Nereye',
      debugShowCheckedModeBanner: false,
      theme: NereyeTheme.light(),
      darkTheme: NereyeTheme.dark(),
      themeMode: ThemeMode.system,
      routerConfig: router,
    );
  }
}
