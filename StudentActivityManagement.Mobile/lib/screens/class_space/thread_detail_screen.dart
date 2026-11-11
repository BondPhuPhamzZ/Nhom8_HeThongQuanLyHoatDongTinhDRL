import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/class_space.dart';
import '../../services/api_client.dart';
import '../../services/class_space_service.dart';
import '../../utils/vn_time.dart';
import '../widgets/activity_card.dart';
import '../widgets/feedback_views.dart';
import '../widgets/status_badge.dart';

/// A class thread with its replies. Members reply and delete their own posts; the monitor pins, marks the
/// answer (US-14) and raises the question to Phòng QL HSSV (US-15).
class ThreadDetailScreen extends StatefulWidget {
  const ThreadDetailScreen({super.key, required this.threadId});

  final int threadId;

  @override
  State<ThreadDetailScreen> createState() => _ThreadDetailScreenState();
}

class _ThreadDetailScreenState extends State<ThreadDetailScreen> {
  final _service = ClassSpaceService();
  final _reply = TextEditingController();
  ThreadDetail? _thread;
  String? _error;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _reply.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final thread = await _service.thread(widget.threadId);
      if (mounted) setState(() => _thread = thread);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  Future<void> _run(Future<String> Function() action, {bool closeAfter = false}) async {
    setState(() => _busy = true);
    try {
      final message = await action();
      if (!mounted) return;
      showMessage(context, message);
      if (closeAfter) {
        Navigator.pop(context, true);
        return;
      }
      await _load();
    } on ApiException catch (error) {
      if (mounted) showMessage(context, error.fullMessage, error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<bool> _confirm(String text) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppStyles.radius)),
          content: Text(text),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Không')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Đồng ý')),
          ],
        ),
      ) ??
      false;

  Future<void> _escalate(ThreadDetail thread) async {
    final controller = TextEditingController(text: thread.summary.title);
    final question = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppStyles.radius)),
        title: const Text('Chuyển lên Phòng QL HSSV'),
        content: TextField(controller: controller, minLines: 3, maxLines: 6, maxLength: 2000, decoration: const InputDecoration(hintText: 'Nội dung cần phòng xác nhận')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text), child: const Text('Chuyển lên')),
        ],
      ),
    );
    controller.dispose();
    if (question == null || question.trim().length < 5) {
      if (question != null && mounted) showMessage(context, 'Nội dung cần xác nhận tối thiểu 5 ký tự.', error: true);
      return;
    }
    await _run(() => _service.escalate(thread.summary.id, question));
  }

  Future<void> _sendReply() async {
    final text = _reply.text.trim();
    if (text.isEmpty) return;
    await _run(() async {
      final message = await _service.reply(widget.threadId, text);
      _reply.clear();
      return message;
    });
  }

  @override
  Widget build(BuildContext context) {
    final thread = _thread;
    return Scaffold(
      appBar: AppBar(
        title: Text(thread?.summary.title ?? 'Chủ đề', overflow: TextOverflow.ellipsis),
        actions: [
          if (thread != null && thread.canPin)
            IconButton(
              tooltip: thread.summary.isPinned ? 'Bỏ ghim' : 'Ghim chủ đề',
              icon: Icon(thread.summary.isPinned ? LucideIcons.pinOff : LucideIcons.pin),
              onPressed: _busy ? null : () => _run(() => _service.setPinned(thread.summary.id, !thread.summary.isPinned)),
            ),
          if (thread != null && thread.canDelete)
            IconButton(
              tooltip: 'Xóa chủ đề',
              icon: const Icon(LucideIcons.trash2),
              onPressed: _busy
                  ? null
                  : () async {
                      if (await _confirm('Xóa chủ đề này?')) await _run(() => _service.deleteThread(thread.summary.id), closeAfter: true);
                    },
            ),
        ],
      ),
      body: _error != null
          ? Padding(padding: const EdgeInsets.all(16), child: ErrorView(message: _error!, onRetry: _load))
          : thread == null
              ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList(count: 3, height: 90))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      EntryAnimation(child: _ThreadBody(thread: thread)),
                      if (thread.escalation case final escalation?)
                        _EscalationCard(
                          escalation: escalation,
                          busy: _busy,
                          onRelay: () => _run(() => _service.relay(escalation.id)),
                        ),
                      if (thread.canEscalate)
                        Padding(
                          padding: const EdgeInsets.only(top: 12),
                          child: OutlinedButton.icon(
                            onPressed: _busy ? null : () => _escalate(thread),
                            icon: const Icon(LucideIcons.arrowUpRight, size: 18),
                            label: const Text('Chuyển lên Phòng QL HSSV'),
                          ),
                        ),
                      SectionTitle('${thread.replies.length} trả lời'),
                      if (thread.replies.isEmpty)
                        const EmptyState(icon: LucideIcons.messageCircle, message: 'Chưa có ai trả lời.')
                      else
                        for (var i = 0; i < thread.replies.length; i++)
                          EntryAnimation(
                            index: i,
                            child: _ReplyCard(
                              reply: thread.replies[i],
                              busy: _busy,
                              onMarkAnswer: () => _run(() => _service.markAnswer(thread.summary.id, thread.replies[i].id)),
                              onDelete: () async {
                                if (await _confirm('Xóa trả lời này?')) await _run(() => _service.deleteReply(thread.summary.id, thread.replies[i].id));
                              },
                            ),
                          ),
                    ],
                  ),
                ),
      bottomNavigationBar: thread == null || thread.summary.status == 'Closed'
          ? null
          : SafeArea(
              child: Container(
                padding: EdgeInsets.fromLTRB(12, 8, 12, 8 + MediaQuery.of(context).viewInsets.bottom),
                decoration: const BoxDecoration(color: AppColors.white, border: Border(top: BorderSide(color: AppColors.border))),
                child: Row(children: [
                  Expanded(
                    child: TextField(
                      controller: _reply,
                      minLines: 1,
                      maxLines: 4,
                      maxLength: 4000,
                      decoration: const InputDecoration(hintText: 'Trả lời của bạn', counterText: ''),
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    tooltip: 'Gửi trả lời',
                    style: IconButton.styleFrom(shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppStyles.radius))),
                    onPressed: _busy ? null : _sendReply,
                    icon: const Icon(LucideIcons.send, size: 18),
                  ),
                ]),
              ),
            ),
    );
  }
}

class _ThreadBody extends StatelessWidget {
  const _ThreadBody({required this.thread});
  final ThreadDetail thread;

  @override
  Widget build(BuildContext context) {
    final summary = thread.summary;
    final escalation = escalationLabel(summary.escalationStatus);
    return InfoPanel(children: [
      Wrap(spacing: 6, runSpacing: 4, children: [
        if (summary.isPinned) const StatusBadge('Pinned', label: 'Ghim'),
        StatusBadge(summary.kind),
        if (summary.status != 'Open') StatusBadge(summary.status),
        if (escalation != null) StatusBadge(summary.escalationStatus == 'Answered' ? 'Accepted' : 'Pending', label: escalation),
      ]),
      const SizedBox(height: 8),
      Text(summary.title, style: Theme.of(context).textTheme.titleMedium),
      Text(
        '${summary.author.fullName} (${summary.author.studentCode})${summary.author.isMonitor ? ' · lớp trưởng' : ''} · ${VnTime.dateTime(summary.createdAt)}',
        style: const TextStyle(color: AppColors.muted, fontSize: 12),
      ),
      const SizedBox(height: 8),
      Text(thread.body),
    ]);
  }
}

class _ReplyCard extends StatelessWidget {
  const _ReplyCard({required this.reply, required this.busy, required this.onMarkAnswer, required this.onDelete});
  final ThreadReply reply;
  final bool busy;
  final VoidCallback onMarkAnswer;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) => Container(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: AppColors.white,
          borderRadius: BorderRadius.circular(AppStyles.radius),
          border: Border.all(color: reply.isAnswer ? AppColors.success : AppColors.border, width: reply.isAnswer ? 1.5 : 1),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(spacing: 6, runSpacing: 4, crossAxisAlignment: WrapCrossAlignment.center, children: [
              Text(reply.author.fullName, style: const TextStyle(fontWeight: FontWeight.w700)),
              if (reply.author.isMonitor) const StatusBadge('Ongoing', label: 'Lớp trưởng'),
              if (reply.isAnswer) const StatusBadge('Accepted', label: 'Câu trả lời'),
            ]),
            Text(VnTime.dateTime(reply.createdAt), style: const TextStyle(color: AppColors.muted, fontSize: 11)),
            const SizedBox(height: 6),
            Text(reply.body),
            if (reply.canMarkAnswer || reply.canDelete)
              Row(mainAxisAlignment: MainAxisAlignment.end, children: [
                if (reply.canMarkAnswer)
                  TextButton.icon(onPressed: busy ? null : onMarkAnswer, icon: const Icon(LucideIcons.checkCheck, size: 16), label: const Text('Chọn làm câu trả lời')),
                if (reply.canDelete)
                  TextButton(
                    style: TextButton.styleFrom(foregroundColor: AppColors.danger),
                    onPressed: busy ? null : onDelete,
                    child: const Text('Xóa'),
                  ),
              ]),
          ],
        ),
      );
}

/// The monitor's escalation on this thread: the question, its state and the official answer with the relay action.
class _EscalationCard extends StatelessWidget {
  const _EscalationCard({required this.escalation, required this.busy, required this.onRelay});

  final Escalation escalation;
  final bool busy;
  final VoidCallback onRelay;

  @override
  Widget build(BuildContext context) {
    final answer = escalation.answer;
    return Card(
      margin: const EdgeInsets.only(top: 12),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              const Expanded(child: Text('Câu hỏi đã chuyển lên Phòng QL HSSV', style: TextStyle(fontWeight: FontWeight.w700))),
              StatusBadge(escalation.status == 'Answered' ? 'Accepted' : escalation.status, label: escalationLabel(escalation.status) ?? escalation.status),
            ]),
            const SizedBox(height: 4),
            Text(escalation.question, style: const TextStyle(color: AppColors.muted, fontSize: 13)),
            if (answer != null) ...[
              const SizedBox(height: 10),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: const Color(0xFFEEF9F1),
                  borderRadius: BorderRadius.circular(AppStyles.radius),
                  border: Border.all(color: const Color(0xFFC3E8CD)),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Phản hồi của ${escalation.answeredByName ?? 'Phòng QL HSSV'}${escalation.answeredAt == null ? '' : ' · ${VnTime.dateTime(escalation.answeredAt)}'}',
                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12),
                    ),
                    const SizedBox(height: 4),
                    Text(answer),
                  ],
                ),
              ),
              const SizedBox(height: 10),
              if (escalation.canRelay)
                FilledButton.icon(
                  onPressed: busy ? null : onRelay,
                  icon: const Icon(LucideIcons.share2, size: 18),
                  label: const Text('Chuyển phản hồi vào lớp'),
                )
              else if (escalation.relayedAt != null)
                Text('Đã chuyển vào lớp · ${VnTime.dateTime(escalation.relayedAt)}', style: const TextStyle(color: AppColors.success, fontSize: 12)),
            ] else if (escalation.status == 'Pending') ...[
              const SizedBox(height: 8),
              const Text('Đang chờ Phòng QL HSSV trả lời. Bạn sẽ nhận thông báo khi có phản hồi.', style: TextStyle(fontSize: 12, color: AppColors.muted)),
            ],
          ],
        ),
      ),
    );
  }
}
