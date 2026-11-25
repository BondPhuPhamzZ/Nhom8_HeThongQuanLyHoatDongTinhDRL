import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../services/api_client.dart';
import '../../services/auth_service.dart';
import '../widgets/feedback_views.dart';

class ResetPasswordScreen extends StatefulWidget {
  const ResetPasswordScreen({super.key, required this.login});

  final String login;

  @override
  State<ResetPasswordScreen> createState() => _ResetPasswordScreenState();
}

class _ResetPasswordScreenState extends State<ResetPasswordScreen> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _login = TextEditingController(text: widget.login);
  final _code = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  final _service = AuthService();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _login.dispose();
    _code.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _reset() async {
    if (_busy || !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final message = await _service.resetPassword(_login.text, _code.text, _password.text);
      if (!mounted) return;
      showMessage(context, message);
      Navigator.popUntil(context, (route) => route.isFirst);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.fullMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Đặt lại mật khẩu')),
        body: Form(
          key: _form,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              TextFormField(
                controller: _login,
                enabled: !_busy,
                decoration: const InputDecoration(labelText: 'Mã tài khoản hoặc email'),
                validator: (text) => (text ?? '').trim().isEmpty ? 'Nhập mã tài khoản hoặc email.' : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _code,
                enabled: !_busy,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                maxLength: 6,
                autofillHints: const [AutofillHints.oneTimeCode],
                decoration: const InputDecoration(labelText: 'Mã xác nhận'),
                validator: (text) => (text ?? '').length != 6 ? 'Nhập đúng 6 chữ số.' : null,
              ),
              TextFormField(
                controller: _password,
                enabled: !_busy,
                obscureText: true,
                maxLength: 64,
                decoration: const InputDecoration(labelText: 'Mật khẩu mới'),
                validator: (text) => (text ?? '').length < 8 ? 'Mật khẩu mới cần ít nhất 8 ký tự.' : null,
              ),
              const Text('Dùng chữ cái và chữ số. Không dùng mật khẩu hiện tại hoặc mã tài khoản.'),
              const SizedBox(height: 12),
              TextFormField(
                controller: _confirm,
                enabled: !_busy,
                obscureText: true,
                decoration: const InputDecoration(labelText: 'Nhập lại mật khẩu mới'),
                validator: (text) => text != _password.text ? 'Mật khẩu nhập lại không khớp.' : null,
              ),
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              const SizedBox(height: 16),
              FilledButton(onPressed: _busy ? null : _reset, child: Text(_busy ? 'Đang lưu…' : 'Đặt lại mật khẩu')),
            ],
          ),
        ),
      );
}
