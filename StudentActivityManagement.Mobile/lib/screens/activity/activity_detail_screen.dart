import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:provider/provider.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/activity.dart';
import '../../providers/auth_provider.dart';
import '../../services/activity_service.dart';
import '../../services/api_client.dart';
import '../../utils/vn_time.dart';
import '../widgets/activity_card.dart';
import '../widgets/feedback_views.dart';
import '../widgets/status_badge.dart';
import '../class_space/thread_form_screen.dart';
import 'participation_sections.dart';

/// US-03 detail with US-04 register and US-05 cancel.
class ActivityDetailScreen extends StatefulWidget {
  const ActivityDetailScreen({super.key, required this.activityId});

  final int activityId;

  @override
  State<ActivityDetailScreen> createState() => _ActivityDetailScreenState();
}

class _ActivityDetailScreenState extends State<ActivityDetailScreen> {
  final _service = ActivityService();
  Activity? _activity;
  String? _error;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final activity = await _service.detail(widget.activityId);
      setState(() => _activity = activity);
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    }
  }

  Future<void> _run(Future<String> Function() action) async {
    setState(() => _busy = true);
    try {
      final message = await action();
      if (mounted) showMessage(context, message);
      await _load();
    } on ApiException catch (error) {
      if (mounted) showMessage(context, error.fullMessage, error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<bool> _confirm(String title, String body) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppStyles.radius)),
          title: Text(title),
          content: Text(body),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Không')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Đồng ý')),
          ],
        ),
      ) ??
      false;

  @override
  Widget build(BuildContext context) {
    final activity = _activity;
    return Scaffold(
      appBar: AppBar(
        title: Text(activity?.name ?? 'Chi tiết hoạt động', overflow: TextOverflow.ellipsis),
        actions: [
          if (activity != null && (context.watch<AuthProvider>().user?.isMonitor ?? false))
            IconButton(tooltip: 'Chuyển tiếp vào lớp', icon: const Icon(LucideIcons.share2), onPressed: () => _forward(activity)),
        ],
      ),
      body: _error != null
          ? Padding(padding: const EdgeInsets.all(16), child: ErrorView(message: _error!, onRetry: _load))
          : activity == null
              ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList(count: 3, height: 140))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      for (final (index, section) in buildSections(activity).indexed) EntryAnimation(index: index, child: section),
                    ],
                  ),
                ),
      bottomNavigationBar: activity == null ? null : _actionBar(activity),
    );
  }

  /// US-13: the monitor forwards the activity to the class as an announcement (same text as the Web).
  Future<void> _forward(Activity activity) async {
    final first = activity.orderedSessions.isEmpty ? null : activity.orderedSessions.first;
    final lines = [
      'Hoạt động: ${activity.name}',
      'Đơn vị tổ chức: ${activity.organizerName}',
      if (first != null) 'Buổi đầu: ${VnTime.dateTime(first.startTime)} tại ${first.location}, ${first.campus}',
      'Hạn đăng ký: ${VnTime.dateTime(activity.registrationCloseTime)}',
      'Điểm rèn luyện: ${activity.trainingPoints}',
      if (activity.requirements.isNotEmpty) 'Cần chuẩn bị: ${activity.requirements}',
    ];
    final title = 'Thông tin hoạt động: ${activity.name}';
    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => ThreadFormScreen(
          isMonitor: true,
          initialKind: 'Announcement',
          initialTitle: title.length > 200 ? title.substring(0, 200) : title,
          initialBody: lines.join('\n'),
        ),
      ),
    );
  }

  /// Content blocks of the screen, in order. Later features add their own blocks here.
  List<Widget> buildSections(Activity activity) => [
        _header(activity),
        _facts(activity),
        if (activity.description.isNotEmpty || activity.requirements.isNotEmpty) _description(activity),
        _sessions(activity),
        if (activity.holdsPlace) ...[
          CheckInSection(key: ValueKey('checkin-${activity.myRegistrationStatus}'), activity: activity, onChanged: _load),
          EvidenceSection(key: ValueKey('evidence-${activity.status}'), activity: activity),
        ],
      ];

  Widget _header(Activity activity) {
    final type = activity.eligibility?.participationType;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(spacing: 6, runSpacing: 6, children: [
            StatusBadge(activity.status),
            if (type != null) StatusBadge(type),
            if (activity.myRegistrationStatus != null && activity.myRegistrationStatus != 'Cancelled') StatusBadge(activity.myRegistrationStatus!),
            PointsChip(activity.trainingPoints),
          ]),
          const SizedBox(height: 10),
          Text(activity.name, style: Theme.of(context).textTheme.titleLarge),
          Text('Đơn vị tổ chức: ${activity.organizerName}', style: const TextStyle(color: AppColors.muted)),
          if (activity.eligibility != null && !activity.eligibility!.isEligible && activity.eligibility!.reason.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(activity.eligibility!.reason, style: const TextStyle(color: AppColors.danger)),
          ],
        ],
      ),
    );
  }

  Widget _facts(Activity activity) => InfoPanel(children: [
        _Fact(icon: LucideIcons.users, label: 'Đã đăng ký', value: '${activity.currentParticipants}/${activity.maxParticipants} · còn ${activity.remainingSlots} chỗ'),
        _Fact(icon: LucideIcons.calendar, label: 'Mở đăng ký', value: VnTime.dateTime(activity.registrationOpenTime)),
        _Fact(icon: LucideIcons.calendarCheck, label: 'Đóng đăng ký', value: VnTime.dateTime(activity.registrationCloseTime)),
        _Fact(icon: LucideIcons.graduationCap, label: 'Đối tượng', value: activity.targetAudience),
      ]);

  Widget _description(Activity activity) => Padding(
        padding: const EdgeInsets.only(top: 12),
        child: InfoPanel(children: [
          if (activity.description.isNotEmpty) Text(activity.description),
          if (activity.requirements.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text('Yêu cầu cần chuẩn bị', style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700)),
            Text(activity.requirements),
          ],
        ]),
      );

  Widget _sessions(Activity activity) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SectionTitle('Lịch tổ chức'),
          for (final session in activity.orderedSessions)
            Card(
              child: ListTile(
                leading: const Icon(LucideIcons.clock, size: 20, color: AppColors.primary),
                title: Text(session.name, style: const TextStyle(fontWeight: FontWeight.w700)),
                subtitle: Text('${VnTime.dateTime(session.startTime)} – ${VnTime.time(session.endTime)}\n${session.campus} · ${session.location}'),
                isThreeLine: true,
              ),
            ),
        ],
      );

  Widget? _actionBar(Activity activity) {
    final now = DateTime.now().toUtc();
    final Widget? button;
    if (activity.canRegisterAt(now)) {
      button = BusyButton(
        label: 'Đăng ký tham gia',
        icon: LucideIcons.ticket,
        busy: _busy,
        onPressed: () => _run(() => _service.register(activity.id)),
      );
    } else if (activity.canCancelAt(now)) {
      button = BusyButton(
        label: 'Hủy đăng ký',
        icon: LucideIcons.x,
        outlined: true,
        busy: _busy,
        onPressed: () async {
          if (await _confirm('Hủy đăng ký?', 'Bạn có thể đăng ký lại khi hoạt động còn hạn và còn chỗ.')) {
            await _run(() => _service.cancel(activity.id));
          }
        },
      );
    } else {
      button = null;
    }
    if (button == null) return null;
    return SafeArea(
      child: Container(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
        decoration: const BoxDecoration(color: AppColors.white, border: Border(top: BorderSide(color: AppColors.border))),
        child: button,
      ),
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.icon, required this.label, required this.value});
  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, size: 16, color: AppColors.muted),
            const SizedBox(width: 8),
            SizedBox(width: 104, child: Text(label, style: const TextStyle(color: AppColors.muted, fontSize: 13))),
            Expanded(child: Text(value, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13))),
          ],
        ),
      );
}
