import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/activity.dart';
import '../../models/participation.dart';
import '../../services/api_client.dart';
import '../../services/participation_service.dart';
import '../../utils/vn_time.dart';
import '../widgets/activity_card.dart';
import '../widgets/feedback_views.dart';
import '../widgets/status_badge.dart';

/// US-07: check-in state of every session; enter the code announced by the organisers while the window is open.
class CheckInSection extends StatefulWidget {
  const CheckInSection({super.key, required this.activity, required this.onChanged});

  final Activity activity;
  final VoidCallback onChanged;

  @override
  State<CheckInSection> createState() => _CheckInSectionState();
}

class _CheckInSectionState extends State<CheckInSection> {
  final _service = ParticipationService();
  CheckInStatus? _status;
  String? _error;
  int? _busySession;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final status = await _service.checkInStatus(widget.activity.id);
      if (mounted) setState(() => _status = status);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  Future<void> _checkIn(SessionCheckIn session) async {
    final controller = TextEditingController();
    final code = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppStyles.radius)),
        title: Text('Check-in ${session.sessionName}'),
        content: TextField(
          controller: controller,
          autofocus: true,
          textCapitalization: TextCapitalization.characters,
          style: AppStyles.mono.copyWith(fontSize: 20),
          decoration: const InputDecoration(hintText: 'Mã do ban tổ chức công bố'),
          onSubmitted: (value) => Navigator.pop(context, value),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text), child: const Text('Check-in')),
        ],
      ),
    );
    controller.dispose();
    if (code == null || code.trim().isEmpty) return;

    setState(() => _busySession = session.sessionId);
    try {
      final message = await _service.checkIn(widget.activity.id, session.sessionId, code);
      if (mounted) showMessage(context, message.isEmpty ? 'Check-in thành công.' : message);
      await _load();
      widget.onChanged();
    } on ApiException catch (error) {
      if (mounted) showMessage(context, error.fullMessage, error: true);
    } finally {
      if (mounted) setState(() => _busySession = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final status = _status;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Check-in'),
        if (_error != null)
          ErrorView(message: _error!, onRetry: _load)
        else if (status == null)
          const SkeletonList(count: 1, height: 72)
        else ...[
          Text(
            status.usesCode
                ? 'Nhập mã của từng buổi trong khung giờ check-in. Mỗi buổi chỉ check-in một lần.'
                : 'Ban tổ chức điểm danh trực tiếp tại buổi; bạn không cần nhập mã.',
            style: const TextStyle(color: AppColors.muted, fontSize: 13),
          ),
          const SizedBox(height: 8),
          for (final session in status.sessions)
            Card(
              child: ListTile(
                leading: Icon(
                  session.checkedInAt != null ? LucideIcons.circleCheck : LucideIcons.scanLine,
                  color: session.checkedInAt != null ? AppColors.success : AppColors.primary,
                ),
                title: Text(session.sessionName, style: const TextStyle(fontWeight: FontWeight.w700)),
                subtitle: Text(session.checkedInAt != null
                    ? 'Đã check-in lúc ${VnTime.dateTime(session.checkedInAt)}'
                    : 'Mở ${VnTime.dateTime(session.opensAt)} – đóng ${VnTime.time(session.closesAt)}'),
                trailing: session.checkedInAt == null && status.usesCode && session.canCheckIn
                    ? FilledButton(
                        onPressed: _busySession == null ? () => _checkIn(session) : null,
                        child: Text(_busySession == session.sessionId ? '...' : 'Nhập mã'),
                      )
                    : null,
              ),
            ),
        ],
      ],
    );
  }
}

/// US-08: evidence submitted for the activity, a form to add one, and deletion while still pending.
class EvidenceSection extends StatefulWidget {
  const EvidenceSection({super.key, required this.activity});

  final Activity activity;

  @override
  State<EvidenceSection> createState() => _EvidenceSectionState();
}

class _EvidenceSectionState extends State<EvidenceSection> {
  final _service = ParticipationService();
  List<Evidence>? _items;
  String? _error;

  /// Same rule as the Web: a place in the activity and the activity has started.
  bool get _canSubmit => widget.activity.holdsPlace && (widget.activity.status == 'Ongoing' || widget.activity.status == 'Completed');

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final items = await _service.evidences(widget.activity.id);
      if (mounted) setState(() => _items = items);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  Future<void> _delete(Evidence item) async {
    try {
      final message = await _service.deleteEvidence(widget.activity.id, item.id);
      if (mounted) showMessage(context, message);
      await _load();
    } on ApiException catch (error) {
      if (mounted) showMessage(context, error.fullMessage, error: true);
    }
  }

  Future<void> _openForm() async {
    final submitted = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(AppStyles.radius))),
      builder: (_) => _EvidenceForm(activity: widget.activity, service: _service),
    );
    if (submitted == true) await _load();
  }

  @override
  Widget build(BuildContext context) {
    final items = _items;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionTitle(
          'Minh chứng tham gia',
          trailing: _canSubmit
              ? TextButton.icon(onPressed: _openForm, icon: const Icon(LucideIcons.upload, size: 16), label: const Text('Nộp minh chứng'))
              : null,
        ),
        if (!_canSubmit)
          const Padding(
            padding: EdgeInsets.only(bottom: 8),
            child: Text('Nộp minh chứng khi hoạt động đang diễn ra hoặc đã kết thúc (trong hạn).', style: TextStyle(color: AppColors.muted, fontSize: 13)),
          ),
        if (_error != null)
          ErrorView(message: _error!, onRetry: _load)
        else if (items == null)
          const SkeletonList(count: 1, height: 72)
        else if (items.isEmpty)
          const EmptyState(icon: LucideIcons.fileText, message: 'Chưa có minh chứng nào được nộp cho hoạt động này.')
        else
          for (final item in items)
            Card(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(children: [
                      StatusBadge(item.reviewStatus),
                      const SizedBox(width: 8),
                      Text('${item.typeLabel} · ${item.sessionName ?? 'Chung'}', style: const TextStyle(fontWeight: FontWeight.w700)),
                      const Spacer(),
                      if (item.canDelete)
                        IconButton(
                          tooltip: 'Xóa minh chứng',
                          icon: const Icon(LucideIcons.trash2, size: 18, color: AppColors.danger),
                          onPressed: () => _delete(item),
                        ),
                    ]),
                    if (item.originalFileName != null)
                      Text('${item.originalFileName}${item.sizeBytes == null ? '' : ' (${(item.sizeBytes! / 1024).toStringAsFixed(1)} KB)'}'),
                    if (item.confirmationCode != null) Text('Mã: ${item.confirmationCode}', style: AppStyles.mono.copyWith(letterSpacing: 1, fontSize: 13)),
                    if (item.note != null && item.note!.isNotEmpty) Text(item.note!, style: const TextStyle(color: AppColors.muted)),
                    if (item.reviewNote != null && item.reviewNote!.isNotEmpty)
                      Text('Phản hồi: ${item.reviewNote}', style: const TextStyle(color: AppColors.charcoal)),
                    Text('Nộp lúc ${VnTime.dateTime(item.uploadedAt)}', style: const TextStyle(color: AppColors.muted, fontSize: 12)),
                  ],
                ),
              ),
            ),
      ],
    );
  }
}

class _EvidenceForm extends StatefulWidget {
  const _EvidenceForm({required this.activity, required this.service});

  final Activity activity;
  final ParticipationService service;

  @override
  State<_EvidenceForm> createState() => _EvidenceFormState();
}

class _EvidenceFormState extends State<_EvidenceForm> {
  static const _maxBytes = 5 * 1024 * 1024;
  final _code = TextEditingController();
  final _note = TextEditingController();
  String _type = 'Image';
  int? _sessionId;
  PlatformFile? _file;
  List<int>? _bytes;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _code.dispose();
    _note.dispose();
    super.dispose();
  }

  Future<void> _pick() async {
    final file = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: _type == 'Image' ? ['jpg', 'jpeg', 'png'] : ['pdf'],
    );
    if (file == null) return;
    final bytes = await file.readAsBytes();
    setState(() {
      _file = file;
      _bytes = bytes;
      _error = bytes.length > _maxBytes ? 'File minh chứng tối đa 5 MB.' : null;
    });
  }

  Future<void> _submit() async {
    final needsFile = _type != 'ConfirmationCode';
    if (needsFile && _bytes == null) {
      setState(() => _error = _type == 'Image' ? 'Chọn ảnh .jpg, .jpeg hoặc .png.' : 'Chọn file .pdf.');
      return;
    }
    if (!needsFile && _code.text.trim().length < 4) {
      setState(() => _error = 'Mã xác nhận tối thiểu 4 ký tự.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final message = await widget.service.submitEvidence(
        widget.activity.id,
        evidenceType: _type,
        sessionId: _sessionId,
        confirmationCode: needsFile ? null : _code.text,
        note: _note.text,
        fileBytes: needsFile ? _bytes : null,
        fileName: needsFile ? _file!.name : null,
      );
      if (!mounted) return;
      showMessage(context, message.isEmpty ? 'Đã nộp minh chứng.' : message);
      Navigator.pop(context, true);
    } on ApiException catch (error) {
      setState(() => _error = error.fullMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final sessions = widget.activity.orderedSessions;
    return Padding(
      padding: EdgeInsets.fromLTRB(16, 16, 16, 16 + MediaQuery.of(context).viewInsets.bottom),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Nộp minh chứng', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            SegmentedButton<String>(
              segments: const [
                ButtonSegment(value: 'Image', label: Text('Ảnh'), icon: Icon(LucideIcons.image, size: 16)),
                ButtonSegment(value: 'Document', label: Text('PDF'), icon: Icon(LucideIcons.fileText, size: 16)),
                ButtonSegment(value: 'ConfirmationCode', label: Text('Mã'), icon: Icon(LucideIcons.qrCode, size: 16)),
              ],
              selected: {_type},
              onSelectionChanged: (value) => setState(() {
                _type = value.first;
                _file = null;
                _bytes = null;
                _error = null;
              }),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<int?>(
              isExpanded: true,
              initialValue: _sessionId,
              decoration: const InputDecoration(labelText: 'Buổi'),
              items: [
                const DropdownMenuItem(value: null, child: Text('Chung cho hoạt động')),
                for (final session in sessions) DropdownMenuItem(value: session.id, child: Text(session.name)),
              ],
              onChanged: (value) => setState(() => _sessionId = value),
            ),
            const SizedBox(height: 12),
            if (_type == 'ConfirmationCode')
              TextField(controller: _code, decoration: const InputDecoration(hintText: 'Mã xác nhận do ban tổ chức cấp'))
            else
              InfoPanel(children: [
                Row(children: [
                  Icon(_type == 'Image' ? LucideIcons.image : LucideIcons.fileText, color: AppColors.muted),
                  const SizedBox(width: 8),
                  Expanded(child: Text(_file?.name ?? (_type == 'Image' ? 'Ảnh .jpg, .jpeg, .png tối đa 5 MB' : 'Tài liệu .pdf tối đa 5 MB'))),
                  OutlinedButton(onPressed: _pick, child: const Text('Chọn file')),
                ]),
              ]),
            const SizedBox(height: 12),
            TextField(controller: _note, maxLength: 500, maxLines: 2, decoration: const InputDecoration(hintText: 'Ghi chú (không bắt buộc)')),
            if (_error != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_error!, style: const TextStyle(color: AppColors.danger))),
            BusyButton(label: 'Nộp minh chứng', icon: LucideIcons.upload, busy: _busy, onPressed: _submit),
          ],
        ),
      ),
    );
  }
}
