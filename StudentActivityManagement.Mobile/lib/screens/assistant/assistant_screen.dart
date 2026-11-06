import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/assistant.dart';
import '../../services/api_client.dart';
import '../../services/assistant_service.dart';
import '../../utils/vn_time.dart';
import '../activity/activity_detail_screen.dart';
import '../widgets/feedback_views.dart';
import '../widgets/status_badge.dart';

/// US-31: chat with the training-point assistant, the Mobile counterpart of the Web's Trợ lý AI page.
class AssistantScreen extends StatefulWidget {
  const AssistantScreen({super.key});

  static const suggestions = [
    'Kỳ này mình còn thiếu bao nhiêu điểm?',
    'Có hoạt động nào mình đăng ký được để bù điểm không?',
    'Mình đã đăng ký những hoạt động nào trong kỳ này?',
    'Khi nào mình check-in được cho hoạt động đã đăng ký?',
  ];

  @override
  State<AssistantScreen> createState() => _AssistantScreenState();
}

class _AssistantScreenState extends State<AssistantScreen> {
  final _service = AssistantService();
  final _input = TextEditingController();
  final _scroll = ScrollController();
  final _messages = <ChatMessage>[];

  AssistantStatus? _status;
  String? _statusError;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _loadStatus();
  }

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _loadStatus() async {
    setState(() => _statusError = null);
    try {
      final status = await _service.status();
      if (mounted) setState(() => _status = status);
    } on ApiException catch (error) {
      if (mounted) setState(() => _statusError = error.message);
    }
  }

  Future<void> _ask(String text) async {
    final question = text.trim();
    if (question.isEmpty || _busy) return;
    _input.clear();
    setState(() {
      _messages.add(ChatMessage.user(question));
      _busy = true;
    });
    _scrollToEnd();

    try {
      final reply = await _service.chat(_messages);
      if (mounted) setState(() => _messages.add(ChatMessage.assistant(reply)));
    } on ApiException catch (error) {
      if (mounted) {
        setState(() {
          // The unanswered question is not sent again, so the next request stays question/answer alternating.
          _messages[_messages.length - 1] = _messages.last.withoutHistory();
          _messages.add(ChatMessage.error(error.message));
        });
      }
    } finally {
      if (mounted) setState(() => _busy = false);
      _scrollToEnd();
    }
  }

  void _reset() => setState(_messages.clear);

  void _scrollToEnd() => WidgetsBinding.instance.addPostFrameCallback((_) {
        if (_scroll.hasClients) {
          _scroll.animateTo(_scroll.position.maxScrollExtent, duration: AppStyles.pageTransition, curve: Curves.easeOut);
        }
      });

  void _openActivity(int id) => Navigator.push(context, MaterialPageRoute(builder: (_) => ActivityDetailScreen(activityId: id)));

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Trợ lý điểm rèn luyện'),
        actions: [
          if (_status?.enabled == true)
            IconButton(
              tooltip: 'Cuộc trò chuyện mới',
              icon: const Icon(LucideIcons.rotateCcw),
              onPressed: _busy || _messages.isEmpty ? null : _reset,
            ),
        ],
      ),
      body: _body(),
    );
  }

  Widget _body() {
    if (_statusError != null) {
      return Padding(padding: const EdgeInsets.all(16), child: ErrorView(message: _statusError!, onRetry: _loadStatus));
    }
    final status = _status;
    if (status == null) return const Padding(padding: EdgeInsets.all(16), child: SkeletonList(count: 3, height: 72));
    if (!status.enabled) {
      return const Padding(
        padding: EdgeInsets.all(16),
        child: EmptyState(
          icon: LucideIcons.sparkles,
          message: 'Trợ lý AI chưa được bật trên máy chủ. Bạn vẫn xem được điểm và hoạt động gợi ý ở tab Điểm.',
        ),
      );
    }

    return Column(
      children: [
        Expanded(
          child: ListView(
            controller: _scroll,
            padding: const EdgeInsets.all(16),
            children: [
              const _PrivacyNotice(),
              const SizedBox(height: 12),
              const ChatBubble(
                message: ChatMessage(
                  role: ChatRole.assistant,
                  text: 'Chào bạn! Mình có thể tra điểm rèn luyện, số điểm còn thiếu, gợi ý hoạt động phù hợp và lịch check-in của bạn.',
                ),
              ),
              for (final message in _messages) ChatBubble(message: message, onOpenActivity: _openActivity),
              if (_busy)
                const ChatBubble(message: ChatMessage(role: ChatRole.assistant, text: 'Đang tra cứu dữ liệu của bạn…'), pending: true),
              if (_messages.isEmpty)
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final question in AssistantScreen.suggestions)
                      ActionChip(label: Text(question), onPressed: _busy ? null : () => _ask(question)),
                  ],
                ),
            ],
          ),
        ),
        _Composer(controller: _input, busy: _busy, onSend: () => _ask(_input.text)),
      ],
    );
  }
}

/// One message: the student's on primary, the assistant's on white, errors outlined in red; the assistant's
/// answers carry tappable activity cards and what was looked up.
class ChatBubble extends StatelessWidget {
  const ChatBubble({super.key, required this.message, this.onOpenActivity, this.pending = false});

  final ChatMessage message;
  final void Function(int activityId)? onOpenActivity;
  final bool pending;

  @override
  Widget build(BuildContext context) {
    final mine = message.role == ChatRole.user;
    final background = mine ? AppColors.primary : AppColors.white;
    final border = message.isError ? AppColors.danger : (mine ? AppColors.primary : AppColors.border);
    final foreground = mine ? AppColors.white : (message.isError ? AppColors.danger : (pending ? AppColors.muted : AppColors.charcoal));

    return EntryAnimation(
      child: Align(
        alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
        child: ConstrainedBox(
          constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * 0.85),
          child: Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: Column(
              crossAxisAlignment: mine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                  decoration: BoxDecoration(
                    color: background,
                    borderRadius: BorderRadius.circular(AppStyles.radius),
                    border: Border.all(color: border),
                    boxShadow: AppStyles.cardShadow,
                  ),
                  child: Text(
                    message.text,
                    style: TextStyle(color: foreground, fontStyle: pending ? FontStyle.italic : FontStyle.normal, height: 1.45),
                  ),
                ),
                for (final activity in message.activities)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: AssistantActivityTile(activity: activity, onTap: onOpenActivity == null ? null : () => onOpenActivity!(activity.id)),
                  ),
                if (message.toolsUsed.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 6),
                    child: Text('Đã tra cứu: ${message.toolLabels.join(', ')}',
                        style: const TextStyle(color: AppColors.muted, fontSize: 12)),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Activity card inside an answer; opens the activity detail (register, check-in, evidence happen there).
class AssistantActivityTile extends StatelessWidget {
  const AssistantActivityTile({super.key, required this.activity, this.onTap});

  final AssistantActivity activity;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final details = [
      if (activity.campus != null) activity.campus!,
      if (activity.startTime != null) 'Bắt đầu ${VnTime.dateTime(activity.startTime)}',
      if (activity.myRegistrationStatus == null && activity.registrationCloseTime != null)
        '${activity.isRegistrationOpen == true ? 'Đóng đăng ký' : 'Đã đóng đăng ký'} ${VnTime.dateTime(activity.registrationCloseTime)}',
    ].join(' · ');

    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: Text(activity.name, style: Theme.of(context).textTheme.titleSmall, maxLines: 2, overflow: TextOverflow.ellipsis)),
                  const SizedBox(width: 8),
                  PointsChip(activity.trainingPoints),
                ],
              ),
              if (details.isNotEmpty) ...[
                const SizedBox(height: 4),
                Text(details, style: const TextStyle(color: AppColors.muted, fontSize: 12)),
              ],
              if (activity.myRegistrationStatus != null) ...[
                const SizedBox(height: 6),
                StatusBadge(activity.myRegistrationStatus!),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _PrivacyNotice extends StatelessWidget {
  const _PrivacyNotice();

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFFE8F6F8),
          borderRadius: BorderRadius.circular(AppStyles.radius),
          border: Border.all(color: AppColors.info),
        ),
        child: const Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(LucideIcons.shieldAlert, size: 18, color: AppColors.info),
            SizedBox(width: 8),
            Expanded(
              child: Text(
                'Câu hỏi được gửi tới Google Gemini (bản miễn phí) và có thể được dùng để cải thiện dịch vụ. '
                'Đừng nhập họ tên, mã sinh viên hay thông tin cá nhân. Trợ lý có thể nhầm, hãy kiểm tra lại ở trang hoạt động.',
                style: TextStyle(fontSize: 12, color: AppColors.charcoal),
              ),
            ),
          ],
        ),
      );
}

class _Composer extends StatelessWidget {
  const _Composer({required this.controller, required this.busy, required this.onSend});

  final TextEditingController controller;
  final bool busy;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) => SafeArea(
        top: false,
        child: Container(
          padding: const EdgeInsets.fromLTRB(16, 8, 8, 8),
          decoration: const BoxDecoration(color: AppColors.white, border: Border(top: BorderSide(color: AppColors.border))),
          child: Row(
            children: [
              Expanded(
                child: TextField(
                  controller: controller,
                  enabled: !busy,
                  minLines: 1,
                  maxLines: 4,
                  maxLength: 1000,
                  textInputAction: TextInputAction.send,
                  onSubmitted: (_) => onSend(),
                  decoration: const InputDecoration(hintText: 'Nhập câu hỏi về điểm rèn luyện…', counterText: ''),
                ),
              ),
              const SizedBox(width: 4),
              IconButton.filled(
                tooltip: 'Gửi',
                onPressed: busy ? null : onSend,
                icon: const Icon(LucideIcons.send),
              ),
            ],
          ),
        ),
      );
}
