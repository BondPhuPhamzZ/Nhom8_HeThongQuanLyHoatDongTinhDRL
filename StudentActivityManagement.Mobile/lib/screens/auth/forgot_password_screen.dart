import 'package:flutter/material.dart';

import '../../services/api_client.dart';
import '../../services/auth_service.dart';
import '../widgets/feedback_views.dart';
import 'reset_password_screen.dart';

class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  final _form = GlobalKey<FormState>();
  final _login = TextEditingController();
  final _service = AuthService();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _login.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    if (_busy || !_form.currentState!.validate()) return;
    final login = _login.text.trim();
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final message = await _service.forgotPassword(login);
      if (!mounted) return;
      showMessage(context, message);
      await Navigator.push(context, MaterialPageRoute(builder: (_) => ResetPasswordScreen(login: login)));
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.fullMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Quên mật khẩu')),
        body: Form(
          key: _form,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              TextFormField(
                controller: _login,
                enabled: !_busy,
                keyboardType: TextInputType.emailAddress,
                decoration: const InputDecoration(labelText: 'Mã tài khoản hoặc email'),
                validator: (text) => (text ?? '').trim().isEmpty ? 'Nhập mã tài khoản hoặc email.' : null,
              ),
              const SizedBox(height: 12),
              const Text('Mã xác nhận được gửi đến email đã đăng ký và có hiệu lực trong 10 phút.'),
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              const SizedBox(height: 16),
              FilledButton(onPressed: _busy ? null : _send, child: Text(_busy ? 'Đang gửi…' : 'Gửi mã xác nhận')),
              TextButton(
                onPressed: _busy
                    ? null
                    : () {
                        final login = _login.text.trim();
                        Navigator.push(context, MaterialPageRoute(builder: (_) => ResetPasswordScreen(login: login)));
                      },
                child: const Text('Đã có mã xác nhận'),
              ),
            ],
          ),
        ),
      );
}
