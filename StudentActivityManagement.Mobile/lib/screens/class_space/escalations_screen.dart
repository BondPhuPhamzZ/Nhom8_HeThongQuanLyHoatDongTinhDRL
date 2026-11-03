import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/class_space.dart';
import '../../services/api_client.dart';
import '../../services/class_space_service.dart';
import '../../utils/vn_time.dart';
import '../widgets/feedback_views.dart';
import '../widgets/status_badge.dart';
import 'thread_detail_screen.dart';

/// Monitor only: questions raised to Phòng QL HSSV (US-15), withdraw while pending, and relay the official
/// answer back into the class thread (US-16).
class EscalationsScreen extends StatefulWidget {
  const EscalationsScreen({super.key});

  @override
  State<EscalationsScreen> createState() => _EscalationsScreenState();
}

class _EscalationsScreenState extends State<EscalationsScreen> {
  static const _filters = {null: 'Tất cả', 'Pending': 'Chờ trả lời', 'Answered': 'Đã trả lời', 'Withdrawn': 'Đã rút lại'};
  final _service = ClassSpaceService();
  String? _status;
  List<Escalation>? _items;
  String? _error;
  int? _busyId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final page = await _service.escalations(status: _status);
      if (mounted) setState(() => _items = page.items);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  Future<void> _run(int id, Future<String> Function() action) async {
    setState(() => _busyId = id);
    try {
      final message = await action();
      if (mounted) showMessage(context, message);
      await _load();
    } on ApiException catch (error) {
      if (mounted) showMessage(context, error.fullMessage, error: true);
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final items = _items;
    return Scaffold(
      appBar: AppBar(title: const Text('Câu hỏi đã chuyển P.QL HSSV')),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Wrap(spacing: 8, children: [
              for (final entry in _filters.entries)
                ChoiceChip(
                  label: Text(entry.value),
                  selected: _status == entry.key,
                  onSelected: (_) {
                    setState(() => _status = entry.key);
                    _load();
                  },
                ),
            ]),
            const SizedBox(height: 12),
            if (_error != null)
              ErrorView(message: _error!, onRetry: _load)
            else if (items == null)
              const SkeletonList(height: 110)
            else if (items.isEmpty)
              const EmptyState(icon: LucideIcons.shieldQuestion, message: 'Chưa có câu hỏi nào được chuyển lên. Mở một chủ đề của lớp và chọn "Chuyển lên Phòng QL HSSV".')
            else
              for (var i = 0; i < items.length; i++) EntryAnimation(index: i, child: _card(items[i])),
          ],
        ),
      ),
    );
  }

  Widget _card(Escalation item) {
    final busy = _busyId == item.id;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              StatusBadge(item.status == 'Answered' ? 'Accepted' : item.status, label: escalationLabel(item.status)),
              const Spacer(),
              Text(VnTime.dateTime(item.createdAt), style: const TextStyle(color: AppColors.muted, fontSize: 12)),
            ]),
            const SizedBox(height: 6),
            InkWell(
              onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => ThreadDetailScreen(threadId: item.threadId))),
              child: Text(item.threadTitle, style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.primary)),
            ),
            const SizedBox(height: 4),
            Text(item.question),
            if (item.answer != null) ...[
              const SizedBox(height: 8),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: const Color(0xFFEAF6EC),
                  borderRadius: BorderRadius.circular(AppStyles.radius),
                  border: Border.all(color: AppColors.success.withValues(alpha: 0.5)),
                ),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text('Phản hồi của ${item.answeredByName ?? 'Phòng QL HSSV'} · ${VnTime.dateTime(item.answeredAt)}',
                      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 12)),
                  const SizedBox(height: 4),
                  Text(item.answer!),
                ]),
              ),
            ],
            const SizedBox(height: 8),
            if (item.canRelay)
              BusyButton(label: 'Chuyển phản hồi vào lớp', icon: LucideIcons.forward, busy: busy, onPressed: () => _run(item.id, () => _service.relay(item.id)))
            else if (item.relayedAt != null)
              Text('Đã chuyển lại lớp bởi ${item.relayedByName ?? 'lớp trưởng'} · ${VnTime.dateTime(item.relayedAt)}', style: const TextStyle(color: AppColors.success, fontSize: 12))
            else if (item.status == 'Pending')
              BusyButton(label: 'Rút lại', icon: LucideIcons.undo2, outlined: true, busy: busy, onPressed: () => _run(item.id, () => _service.withdraw(item.id))),
          ],
        ),
      ),
    );
  }
}
