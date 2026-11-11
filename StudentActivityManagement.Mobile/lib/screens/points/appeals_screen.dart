import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/student_report.dart';
import '../../services/api_client.dart';
import '../../services/report_service.dart';
import '../../utils/vn_time.dart';
import '../widgets/feedback_views.dart';
import '../widgets/status_badge.dart';

/// US-37: decisions of the period that can be appealed now, and every appeal the student sent.
class AppealsScreen extends StatefulWidget {
  const AppealsScreen({super.key, required this.periodId});

  final int periodId;

  @override
  State<AppealsScreen> createState() => _AppealsScreenState();
}

class _AppealsScreenState extends State<AppealsScreen> {
  final _service = ReportService();
  List<ActivityHistoryItem> _appealable = [];
  List<PointAppeal> _appeals = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final history = await _service.history(widget.periodId);
      final appeals = await _service.appeals();
      setState(() {
        _appealable = history.where((item) => item.canAppeal && item.pointRecordId != null).toList();
        _appeals = appeals;
      });
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _appeal(ActivityHistoryItem item) async {
    final sent = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => _AppealForm(item: item, service: _service),
    );
    if (sent == true) await _load();
  }

  Future<void> _withdraw(PointAppeal appeal) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Rút khiếu nại?'),
        content: Text('Khiếu nại cho "${appeal.activityName}" sẽ được rút lại.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Không')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Rút khiếu nại')),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      final message = await _service.withdrawAppeal(appeal.id);
      if (!mounted) return;
      showMessage(context, message.isEmpty ? 'Đã rút khiếu nại.' : message);
      await _load();
    } on ApiException catch (error) {
      if (mounted) showMessage(context, error.fullMessage, error: true);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Khiếu nại điểm')),
        body: _loading && _appeals.isEmpty && _appealable.isEmpty
            ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList(count: 3, height: 110))
            : _error != null
                ? Padding(padding: const EdgeInsets.all(16), child: ErrorView(message: _error!, onRetry: _load))
                : RefreshIndicator(
                    onRefresh: _load,
                    child: ListView(
                      padding: const EdgeInsets.all(16),
                      children: [
                        const Text('Quyết định điểm bị từ chối được khiếu nại trong 14 ngày khi kỳ còn mở.', style: TextStyle(color: AppColors.muted)),
                        const SectionTitle('Có thể khiếu nại'),
                        if (_appealable.isEmpty)
                          const EmptyState(icon: LucideIcons.circleCheck, message: 'Không có quyết định điểm nào cần khiếu nại trong kỳ này.')
                        else
                          for (final item in _appealable)
                            Card(
                              child: Padding(
                                padding: const EdgeInsets.all(14),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(children: [
                                      Expanded(child: Text(item.activityName, style: const TextStyle(fontWeight: FontWeight.w700))),
                                      if (item.pointStatus != null) StatusBadge(item.pointStatus!),
                                    ]),
                                    if (item.pointReason != null && item.pointReason!.isNotEmpty) ...[
                                      const SizedBox(height: 4),
                                      Text('Lý do: ${item.pointReason}', style: const TextStyle(color: AppColors.muted, fontSize: 13)),
                                    ],
                                    const SizedBox(height: 10),
                                    FilledButton.icon(
                                      onPressed: () => _appeal(item),
                                      icon: const Icon(LucideIcons.messageSquareWarning, size: 18),
                                      label: const Text('Gửi khiếu nại'),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                        const SectionTitle('Khiếu nại đã gửi'),
                        if (_appeals.isEmpty)
                          const EmptyState(icon: LucideIcons.inbox, message: 'Bạn chưa gửi khiếu nại nào.')
                        else
                          for (final appeal in _appeals) _AppealCard(appeal: appeal, onWithdraw: () => _withdraw(appeal)),
                      ],
                    ),
                  ),
      );
}

class _AppealCard extends StatelessWidget {
  const _AppealCard({required this.appeal, required this.onWithdraw});

  final PointAppeal appeal;
  final VoidCallback onWithdraw;

  @override
  Widget build(BuildContext context) {
    final accepted = appeal.status == 'Accepted';
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              Expanded(child: Text(appeal.activityName, style: const TextStyle(fontWeight: FontWeight.w700))),
              StatusBadge(appeal.status, label: PointAppeal.labelOf(appeal.status)),
            ]),
            const SizedBox(height: 2),
            Text('Gửi lúc ${VnTime.dateTime(appeal.createdAt)}', style: const TextStyle(color: AppColors.muted, fontSize: 12)),
            const SizedBox(height: 8),
            Text(appeal.reason),
            if (appeal.staffResponse != null && appeal.staffResponse!.isNotEmpty) ...[
              const SizedBox(height: 10),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: accepted ? const Color(0xFFEEF9F1) : AppColors.surface,
                  borderRadius: BorderRadius.circular(AppStyles.radius),
                  border: Border.all(color: accepted ? const Color(0xFFC3E8CD) : AppColors.border),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Phản hồi của Phòng QL HSSV${appeal.resolvedAt == null ? '' : ' · ${VnTime.dateTime(appeal.resolvedAt)}'}',
                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12),
                    ),
                    const SizedBox(height: 4),
                    Text(appeal.staffResponse!),
                  ],
                ),
              ),
            ],
            if (appeal.isPending) ...[
              const SizedBox(height: 10),
              OutlinedButton(onPressed: onWithdraw, child: const Text('Rút khiếu nại')),
            ],
          ],
        ),
      ),
    );
  }
}

class _AppealForm extends StatefulWidget {
  const _AppealForm({required this.item, required this.service});

  final ActivityHistoryItem item;
  final ReportService service;

  @override
  State<_AppealForm> createState() => _AppealFormState();
}

class _AppealFormState extends State<_AppealForm> {
  final _reason = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final reason = _reason.text.trim();
    if (reason.length < 10 || reason.length > 1000) {
      setState(() => _error = 'Lý do khiếu nại phải dài từ 10 đến 1000 ký tự.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final message = await widget.service.appeal(widget.item.pointRecordId!, reason);
      if (!mounted) return;
      showMessage(context, message.isEmpty ? 'Đã gửi khiếu nại.' : message);
      Navigator.pop(context, true);
    } on ApiException catch (error) {
      setState(() => _error = error.fullMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Padding(
        padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + MediaQuery.viewInsetsOf(context).bottom),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Khiếu nại điểm', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 4),
            Text(widget.item.activityName, style: const TextStyle(color: AppColors.muted)),
            const SizedBox(height: 12),
            TextField(
              controller: _reason,
              minLines: 3,
              maxLines: 6,
              maxLength: 1000,
              decoration: const InputDecoration(hintText: 'Lý do khiếu nại (10–1000 ký tự)'),
            ),
            if (_error != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_error!, style: const TextStyle(color: AppColors.danger))),
            BusyButton(label: 'Gửi khiếu nại', icon: LucideIcons.send, busy: _busy, onPressed: _submit),
          ],
        ),
      );
}
