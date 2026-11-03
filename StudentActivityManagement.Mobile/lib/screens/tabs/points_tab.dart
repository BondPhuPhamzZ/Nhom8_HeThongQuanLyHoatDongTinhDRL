import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/student_report.dart';
import '../../services/api_client.dart';
import '../../services/report_service.dart';
import '../../utils/vn_time.dart';
import '../activity/activity_detail_screen.dart';
import '../widgets/activity_card.dart';
import '../widgets/feedback_views.dart';
import '../widgets/status_badge.dart';

/// US-11 points against the period target with recommendations, and US-10 history of the period.
class PointsTab extends StatefulWidget {
  const PointsTab({super.key});

  @override
  State<PointsTab> createState() => _PointsTabState();
}

class _PointsTabState extends State<PointsTab> {
  static const _statuses = {null: 'Mọi trạng thái', 'Registered': 'Đã đăng ký', 'Attended': 'Đã tham gia', 'Cancelled': 'Đã hủy', 'Absent': 'Vắng mặt'};
  final _service = ReportService();

  List<EvaluationPeriod> _periods = [];
  int? _periodId;
  String? _status;
  PointsSummary? _summary;
  List<ActivityHistoryItem> _history = [];
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
      final periods = _periods.isEmpty ? await _service.periods() : _periods;
      final periodId = _periodId ??
          periods.where((period) => period.status == 'Open').map((period) => period.id).firstOrNull ??
          periods.map((period) => period.id).firstOrNull;
      final summary = await _service.points(periodId);
      final history = periodId == null ? <ActivityHistoryItem>[] : await _service.history(periodId, registrationStatus: _status);
      setState(() {
        _periods = periods;
        _periodId = periodId;
        _summary = summary;
        _history = history;
      });
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _openActivity(int id) =>
      Navigator.push(context, MaterialPageRoute(builder: (_) => ActivityDetailScreen(activityId: id))).then((_) => _load());

  @override
  Widget build(BuildContext context) {
    if (_loading && _summary == null && _history.isEmpty) {
      return const Padding(padding: EdgeInsets.all(16), child: SkeletonList(count: 3, height: 120));
    }
    if (_error != null) return Padding(padding: const EdgeInsets.all(16), child: ErrorView(message: _error!, onRetry: _load));
    if (_periods.isEmpty) {
      return const Padding(
        padding: EdgeInsets.all(16),
        child: EmptyState(
          icon: LucideIcons.chartColumn,
          message: 'Chưa có kỳ đánh giá. Điểm và gợi ý hoạt động sẽ hiện khi Phòng QL HSSV mở kỳ đánh giá.',
        ),
      );
    }

    final summary = _summary;
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          DropdownButtonFormField<int>(
            initialValue: _periodId,
            decoration: const InputDecoration(prefixIcon: Icon(LucideIcons.calendar, size: 18)),
            items: [
              for (final period in _periods)
                DropdownMenuItem(value: period.id, child: Text('${period.name} · ${period.approvedPoints}/${period.targetPoints} điểm', overflow: TextOverflow.ellipsis)),
            ],
            onChanged: (value) {
              setState(() => _periodId = value);
              _load();
            },
          ),
          const SizedBox(height: 12),
          if (summary != null) ...[
            EntryAnimation(child: _SummaryCard(summary: summary)),
            if (summary.isAtRisk) ...[
              const SectionTitle('Gợi ý hoạt động bổ sung điểm'),
              if (summary.recommended.isEmpty)
                const EmptyState(icon: LucideIcons.sparkles, message: 'Hiện chưa có hoạt động phù hợp đang mở đăng ký trong kỳ này. Hãy kiểm tra lại sau.')
              else
                for (var i = 0; i < summary.recommended.length; i++)
                  EntryAnimation(index: i, child: ActivityCard(activity: summary.recommended[i], onTap: () => _openActivity(summary.recommended[i].id))),
            ],
          ],
          SectionTitle(
            'Lịch sử trong kỳ',
            trailing: DropdownButton<String?>(
              value: _status,
              underline: const SizedBox.shrink(),
              items: [for (final entry in _statuses.entries) DropdownMenuItem(value: entry.key, child: Text(entry.value, style: const TextStyle(fontSize: 13)))],
              onChanged: (value) {
                setState(() => _status = value);
                _load();
              },
            ),
          ),
          if (_history.isEmpty)
            EmptyState(icon: LucideIcons.history, message: 'Chưa có đăng ký hoạt động trong kỳ này.')
          else
            for (var i = 0; i < _history.length; i++) EntryAnimation(index: i, child: _HistoryCard(item: _history[i], onTap: () => _openActivity(_history[i].activityId))),
        ],
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.summary});
  final PointsSummary summary;

  @override
  Widget build(BuildContext context) => InfoPanel(children: [
        Text('Kỳ ${summary.periodName}', style: const TextStyle(color: AppColors.muted)),
        const SizedBox(height: 8),
        Row(children: [
          _Stat(value: '${summary.approvedPoints}', label: 'Điểm đã duyệt', color: AppColors.primary),
          _Stat(value: '${summary.targetPoints}', label: 'Mục tiêu kỳ', color: AppColors.charcoal),
          _Stat(value: '${summary.missingPoints}', label: 'Còn thiếu', color: summary.isAtRisk ? AppColors.danger : AppColors.success),
        ]),
        const SizedBox(height: 12),
        ClipRRect(
          borderRadius: BorderRadius.circular(AppStyles.radius),
          child: LinearProgressIndicator(
            value: summary.progress,
            minHeight: 8,
            backgroundColor: AppColors.skeleton,
            color: summary.isAtRisk ? AppColors.accent : AppColors.success,
          ),
        ),
        const SizedBox(height: 10),
        Row(children: [
          Icon(summary.isAtRisk ? LucideIcons.circleAlert : LucideIcons.circleCheck, size: 18, color: summary.isAtRisk ? AppColors.accent : AppColors.success),
          const SizedBox(width: 8),
          Expanded(
            child: Text(summary.isAtRisk
                ? 'Bạn đang có nguy cơ thiếu ${summary.missingPoints} điểm. Các hoạt động gợi ý còn mở đăng ký, còn chỗ và phù hợp với bạn.'
                : 'Bạn đã đạt mục tiêu điểm của kỳ này.'),
          ),
        ]),
      ]);
}

class _Stat extends StatelessWidget {
  const _Stat({required this.value, required this.label, required this.color});
  final String value;
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(value, style: TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: color)),
            Text(label, style: const TextStyle(color: AppColors.muted, fontSize: 12, fontWeight: FontWeight.w500)),
          ],
        ),
      );
}

class _HistoryCard extends StatelessWidget {
  const _HistoryCard({required this.item, required this.onTap});
  final ActivityHistoryItem item;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(spacing: 6, runSpacing: 4, children: [
                  StatusBadge(item.registrationStatus),
                  if (item.pointStatus != null) StatusBadge(item.pointStatus!),
                  PointsChip(item.pointsAwarded ?? item.trainingPoints),
                ]),
                const SizedBox(height: 6),
                Text(item.activityName, style: const TextStyle(fontWeight: FontWeight.w700)),
                Text('${item.organizerName} · đăng ký ${VnTime.date(item.registeredAt)}', style: const TextStyle(color: AppColors.muted, fontSize: 12)),
                const SizedBox(height: 4),
                for (final session in item.sessions)
                  Text(
                    '${session.sessionName}: ${session.checkedInAt == null ? 'chưa check-in' : 'check-in ${VnTime.dateTime(session.checkedInAt)}'}',
                    style: const TextStyle(fontSize: 12),
                  ),
                if (item.pointReason != null && item.pointReason!.isNotEmpty)
                  Text('Lý do: ${item.pointReason}', style: const TextStyle(fontSize: 12, color: AppColors.muted)),
              ],
            ),
          ),
        ),
      );
}
