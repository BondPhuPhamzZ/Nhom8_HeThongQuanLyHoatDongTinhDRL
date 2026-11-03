import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../services/api_client.dart';
import '../../services/class_space_service.dart';
import '../widgets/feedback_views.dart';

/// New question or discussion; the monitor may also post an announcement (US-13), for example to forward
/// an activity to the class with [initialTitle]/[initialBody] prefilled.
class ThreadFormScreen extends StatefulWidget {
  const ThreadFormScreen({super.key, required this.isMonitor, this.initialKind = 'Question', this.initialTitle = '', this.initialBody = ''});

  final bool isMonitor;
  final String initialKind;
  final String initialTitle;
  final String initialBody;

  @override
  State<ThreadFormScreen> createState() => _ThreadFormScreenState();
}

class _ThreadFormScreenState extends State<ThreadFormScreen> {
  final _formKey = GlobalKey<FormState>();
  final _service = ClassSpaceService();
  late final _title = TextEditingController(text: widget.initialTitle);
  late final _body = TextEditingController(text: widget.initialBody);
  late String _kind = widget.initialKind;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final (id, message) = await _service.create(kind: _kind, title: _title.text, body: _body.text);
      if (!mounted) return;
      showMessage(context, message);
      Navigator.pop(context, id);
    } on ApiException catch (error) {
      setState(() => _error = error.fullMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Đăng bài trong lớp')),
        body: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              SegmentedButton<String>(
                segments: [
                  const ButtonSegment(value: 'Question', label: Text('Câu hỏi'), icon: Icon(LucideIcons.circleHelp, size: 16)),
                  const ButtonSegment(value: 'Discussion', label: Text('Thảo luận'), icon: Icon(LucideIcons.messagesSquare, size: 16)),
                  if (widget.isMonitor)
                    const ButtonSegment(value: 'Announcement', label: Text('Thông báo'), icon: Icon(LucideIcons.megaphone, size: 16)),
                ],
                selected: {_kind},
                onSelectionChanged: (value) => setState(() => _kind = value.first),
              ),
              const SizedBox(height: 16),
              const Text('Tiêu đề', style: TextStyle(fontWeight: FontWeight.w500)),
              const SizedBox(height: 6),
              TextFormField(
                controller: _title,
                maxLength: 200,
                decoration: const InputDecoration(hintText: 'Ví dụ: Lịch sinh hoạt lớp tuần này?'),
                validator: (value) => (value ?? '').trim().length < 3 ? 'Tiêu đề từ 3 đến 200 ký tự.' : null,
              ),
              const SizedBox(height: 8),
              const Text('Nội dung', style: TextStyle(fontWeight: FontWeight.w500)),
              const SizedBox(height: 6),
              TextFormField(
                controller: _body,
                maxLength: 4000,
                minLines: 5,
                maxLines: 12,
                validator: (value) => (value ?? '').trim().isEmpty ? 'Nhập nội dung.' : null,
              ),
              if (_error != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
              BusyButton(label: 'Đăng', icon: LucideIcons.send, busy: _busy, onPressed: _submit),
            ],
          ),
        ),
      );
}
