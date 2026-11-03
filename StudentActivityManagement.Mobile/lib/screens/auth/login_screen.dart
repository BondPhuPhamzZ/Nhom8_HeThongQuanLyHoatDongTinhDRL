import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:provider/provider.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../providers/auth_provider.dart';
import '../../services/api_client.dart';
import '../../services/preference_service.dart';
import '../widgets/feedback_views.dart';
import 'splash_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _username = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  bool _hidePassword = true;
  String? _error;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await context.read<AuthProvider>().login(_username.text, _password.text);
    } on ApiException catch (error) {
      setState(() => _error = error.fullMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _editServer() async {
    final controller = TextEditingController(text: PreferenceService.instance.baseUrl);
    final saved = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppStyles.radius)),
        title: const Text('Địa chỉ API'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Máy ảo Android: http://10.0.2.2:5210. Máy thật: địa chỉ LAN của máy chạy API.'),
            const SizedBox(height: 12),
            TextField(controller: controller, keyboardType: TextInputType.url, decoration: const InputDecoration(hintText: 'http://10.0.2.2:5210')),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, ''), child: const Text('Mặc định')),
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text), child: const Text('Lưu')),
        ],
      ),
    );
    controller.dispose();
    if (saved == null) return;
    await PreferenceService.instance.setBaseUrl(saved);
    if (mounted) setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final notice = context.watch<AuthProvider>().notice;
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: EntryAnimation(
                child: Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Align(alignment: Alignment.centerLeft, child: BrandMark(size: 44)),
                      const SizedBox(height: 16),
                      Text('Đăng nhập', style: Theme.of(context).textTheme.headlineSmall),
                      const SizedBox(height: 4),
                      const Text('Dành cho sinh viên và lớp trưởng. Dùng mã sinh viên hoặc email do Phòng QL HSSV cấp.',
                          style: TextStyle(color: AppColors.muted)),
                      const SizedBox(height: 20),
                      if (notice != null || _error != null) ...[
                        _Banner(message: _error ?? notice!),
                        const SizedBox(height: 12),
                      ],
                      const _Label('Mã đăng nhập hoặc email'),
                      TextFormField(
                        controller: _username,
                        textInputAction: TextInputAction.next,
                        autofillHints: const [AutofillHints.username],
                        decoration: const InputDecoration(hintText: 'SV210002', prefixIcon: Icon(LucideIcons.user, size: 18)),
                        validator: (value) => (value ?? '').trim().isEmpty ? 'Nhập mã đăng nhập hoặc email.' : null,
                      ),
                      const SizedBox(height: 12),
                      const _Label('Mật khẩu'),
                      TextFormField(
                        controller: _password,
                        obscureText: _hidePassword,
                        autofillHints: const [AutofillHints.password],
                        onFieldSubmitted: (_) => _submit(),
                        decoration: InputDecoration(
                          prefixIcon: const Icon(LucideIcons.lock, size: 18),
                          suffixIcon: IconButton(
                            tooltip: _hidePassword ? 'Hiện mật khẩu' : 'Ẩn mật khẩu',
                            icon: Icon(_hidePassword ? LucideIcons.eye : LucideIcons.eyeOff, size: 18),
                            onPressed: () => setState(() => _hidePassword = !_hidePassword),
                          ),
                        ),
                        validator: (value) => (value ?? '').isEmpty ? 'Nhập mật khẩu.' : null,
                      ),
                      const SizedBox(height: 20),
                      BusyButton(label: 'Đăng nhập', icon: LucideIcons.logIn, busy: _busy, onPressed: _submit),
                      const SizedBox(height: 16),
                      TextButton.icon(
                        onPressed: _editServer,
                        icon: const Icon(LucideIcons.server, size: 16),
                        label: Text('API: ${PreferenceService.instance.baseUrl}', overflow: TextOverflow.ellipsis),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Label extends StatelessWidget {
  const _Label(this.text);
  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Text(text, style: const TextStyle(fontWeight: FontWeight.w500, fontSize: 14)),
      );
}

class _Banner extends StatelessWidget {
  const _Banner({required this.message});
  final String message;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFFFBE9EB),
          borderRadius: BorderRadius.circular(AppStyles.radius),
          border: Border.all(color: AppColors.danger.withValues(alpha: 0.4)),
        ),
        child: Text(message, style: const TextStyle(color: AppColors.danger)),
      );
}
