import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:nereye/api.dart';

class AuthScreen extends ConsumerStatefulWidget {
  const AuthScreen({super.key});

  @override
  ConsumerState<AuthScreen> createState() => _AuthScreenState();
}

class _AuthScreenState extends ConsumerState<AuthScreen> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _name = TextEditingController(text: 'Gezgin');
  var _login = true;
  String? _error;
  var _busy = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 32),
              Text('Nereye', style: Theme.of(context).textTheme.displaySmall?.copyWith(fontWeight: FontWeight.w800)),
              const Text('Karar 30 saniye.'),
              const SizedBox(height: 32),
              if (!_login) TextField(controller: _name, decoration: const InputDecoration(labelText: 'Ad')),
              TextField(controller: _email, decoration: const InputDecoration(labelText: 'E-posta'), keyboardType: TextInputType.emailAddress),
              TextField(controller: _password, decoration: const InputDecoration(labelText: 'Şifre'), obscureText: true),
              if (_error != null) Padding(padding: const EdgeInsets.only(top: 8), child: Text(_error!, style: const TextStyle(color: Colors.red))),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _busy ? null : _submit,
                child: Text(_login ? 'Giriş' : 'Kayıt'),
              ),
              TextButton(
                onPressed: () => setState(() => _login = !_login),
                child: Text(_login ? 'Hesap oluştur' : 'Zaten hesabım var'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _submit() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final api = ref.read(apiProvider);
      final tokens = _login
          ? await api.login(_email.text.trim(), _password.text)
          : await api.register(_email.text.trim(), _password.text, _name.text.trim());
      await ref.read(sessionProvider.notifier).save(tokens);
      if (mounted) {
        context.go('/onboarding');
      }
    } catch (e) {
      setState(() => _error = 'Giriş başarısız. API çalışıyor mu?');
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }
}
