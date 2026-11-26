import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../providers/auth_provider.dart';
import '../../services/api_client.dart';
import '../widgets/feedback_views.dart';

class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key, this.requiredChange = false});

  final bool requiredChange;

  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _form = GlobalKey<FormState>();
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _confirm = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_busy || !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await context.read<AuthProvider>().changePassword(_current.text, _next.text);
      if (!mounted) return;
      showMessage(context, 'Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.');
      if (!widget.requiredChange) Navigator.pop(context);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.fullMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('Đổi mật khẩu'),
          automaticallyImplyLeading: !widget.requiredChange,
          actions: widget.requiredChange
              ? [TextButton(onPressed: _busy ? null : () => context.read<AuthProvider>().logout(), child: const Text('Đăng xuất'))]
              : null,
        ),
        body: Form(
          key: _form,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (widget.requiredChange) ...[
                const Text('Bạn cần đổi mật khẩu tạm trước khi dùng các chức năng khác.'),
                const SizedBox(height: 12),
              ],
              TextFormField(
                controller: _current,
                enabled: !_busy,
                obscureText: true,
                decoration: const InputDecoration(labelText: 'Mật khẩu hiện tại'),
                validator: (text) => (text ?? '').isEmpty ? 'Nhập mật khẩu hiện tại.' : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _next,
                enabled: !_busy,
                obscureText: true,
                maxLength: 64,
                decoration: const InputDecoration(labelText: 'Mật khẩu mới'),
                validator: (text) => (text ?? '').length < 8 ? 'Mật khẩu mới cần ít nhất 8 ký tự.' : null,
              ),
              const Text('Mật khẩu mới cần có chữ cái và chữ số, khác mật khẩu hiện tại và không chứa mã tài khoản.'),
              const SizedBox(height: 12),
              TextFormField(
                controller: _confirm,
                enabled: !_busy,
                obscureText: true,
                decoration: const InputDecoration(labelText: 'Nhập lại mật khẩu mới'),
                validator: (text) => text != _next.text ? 'Mật khẩu nhập lại không khớp.' : null,
              ),
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              const SizedBox(height: 16),
              BusyButton(label: 'Đổi mật khẩu', busy: _busy, onPressed: _save),
            ],
          ),
        ),
      );
}
