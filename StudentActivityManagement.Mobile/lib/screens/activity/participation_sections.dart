import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/activity.dart';
import '../../models/participation.dart';
import '../../services/api_client.dart';
import '../../services/participation_service.dart';
import '../../utils/checkin_qr.dart';
import '../../utils/vn_time.dart';
import 'qr_scan_screen.dart';
import '../widgets/feedback_views.dart';

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

  static const _scanRequest = '\u0000scan';

  Future<void> _checkIn(SessionCheckIn session) async {
    var typedCode = '';
    var code = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppStyles.radius)),
        title: Text('Check-in ${session.sessionName}'),
        content: TextField(
          autofocus: true,
          onChanged: (value) => typedCode = value,
          textCapitalization: TextCapitalization.characters,
          style: AppStyles.mono.copyWith(fontSize: 20),
          decoration: const InputDecoration(hintText: 'Nhập mã check-in'),
          onSubmitted: (value) => Navigator.pop(context, value),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
          TextButton.icon(
            onPressed: () => Navigator.pop(context, _scanRequest),
            icon: const Icon(LucideIcons.scanQrCode, size: 18),
            label: const Text('Quét QR'),
          ),
          FilledButton(onPressed: () => Navigator.pop(context, typedCode), child: const Text('Check-in')),
        ],
      ),
    );
    if (!mounted) return;
    if (code == _scanRequest) {
      // US-35: the QR only supplies the code; it must belong to this activity and this session.
      final qr = await Navigator.push<CheckInQr>(context, MaterialPageRoute(builder: (_) => const QrScanScreen()));
      if (qr == null || !mounted) return;
      if (qr.activityId != widget.activity.id || qr.sessionId != session.sessionId) {
        showMessage(context, 'Mã QR này thuộc buổi hoặc hoạt động khác. Hãy quét đúng mã của ${session.sessionName}.', error: true);
        return;
      }
      code = qr.code;
    }
    if (code == null || code.trim().isEmpty) return;

    setState(() => _busySession = session.sessionId);
    try {
      final message = await _service.checkIn(widget.activity.id, session.sessionId, code);
      if (mounted) showMessage(context, message.isEmpty ? 'Check-in thành công.' : message);
      await _load();
      if (mounted) widget.onChanged();
    } on ApiException catch (error) {
      if (mounted) showMessage(context, error.fullMessage, error: true);
    } finally {
      if (mounted) setState(() => _busySession = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final status = _status;
    final colors = Theme.of(context).colorScheme;
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
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  ListTile(
                    leading: Icon(
                      session.checkedInAt != null ? LucideIcons.circleCheck : LucideIcons.scanLine,
                      color: colors.primary,
                    ),
                    title: Text(session.sessionName, style: const TextStyle(fontWeight: FontWeight.w700)),
                    subtitle: Text([
                      if (session.checkedInAt != null)
                        'Đã check-in lúc ${VnTime.dateTime(session.checkedInAt)}'
                      else
                        'Mở ${VnTime.dateTime(session.opensAt)} – đóng ${VnTime.time(session.closesAt)}',
                      if (session.voidReason != null) 'Đã hủy ghi nhận: ${session.voidReason}',
                    ].join('\n')),
                  ),
                  if (session.checkedInAt == null && status.usesCode && session.canCheckIn)
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                      child: BusyButton(
                        label: 'Nhập mã',
                        busy: _busySession == session.sessionId,
                        onPressed: _busySession == null ? () => _checkIn(session) : null,
                      ),
                    ),
                ],
              ),
            ),
        ],
      ],
    );
  }
}
