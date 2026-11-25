import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/user.dart';
import '../../providers/auth_provider.dart';
import '../../services/api_client.dart';
import '../widgets/feedback_views.dart';

class EditContactScreen extends StatefulWidget {
  const EditContactScreen({super.key, required this.user});

  final AppUser user;

  @override
  State<EditContactScreen> createState() => _EditContactScreenState();
}

class _EditContactScreenState extends State<EditContactScreen> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _email = TextEditingController(text: widget.user.email);
  late final TextEditingController _phone = TextEditingController(text: widget.user.phone);
  final _password = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _phone.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_busy || !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await context.read<AuthProvider>().updateContact(_email.text, _phone.text, _password.text);
      if (!mounted) return;
      showMessage(context, 'Đã cập nhật thông tin liên hệ.');
      Navigator.pop(context);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.fullMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Sửa thông tin liên hệ')),
        body: Form(
          key: _form,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text(widget.user.fullName, style: Theme.of(context).textTheme.titleMedium),
              Text(widget.user.studentCode),
              const SizedBox(height: 16),
              TextFormField(
                controller: _email,
                enabled: !_busy,
                keyboardType: TextInputType.emailAddress,
                decoration: const InputDecoration(labelText: 'Email'),
                validator: (text) => (text ?? '').trim().isEmpty ? 'Nhập email.' : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _phone,
                enabled: !_busy,
                keyboardType: TextInputType.phone,
                decoration: const InputDecoration(labelText: 'Số điện thoại'),
                validator: (text) => (text ?? '').trim().isEmpty ? 'Nhập số điện thoại.' : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _password,
                enabled: !_busy,
                obscureText: true,
                decoration: const InputDecoration(labelText: 'Mật khẩu hiện tại'),
                validator: (text) => (text ?? '').isEmpty ? 'Nhập mật khẩu hiện tại.' : null,
              ),
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              const SizedBox(height: 16),
              FilledButton(onPressed: _busy ? null : _save, child: Text(_busy ? 'Đang lưu…' : 'Lưu thông tin')),
            ],
          ),
        ),
      );
}
