import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../models/class_space.dart';
import '../../services/api_client.dart';
import '../../services/class_space_service.dart';
import '../../utils/vn_time.dart';
import '../class_space/escalations_screen.dart';
import '../class_space/thread_detail_screen.dart';
import '../class_space/thread_form_screen.dart';
import '../widgets/feedback_views.dart';
import '../widgets/status_badge.dart';

/// US-09: the class's private space. Pinned threads first, then the latest activity.
class ClassTab extends StatefulWidget {
  const ClassTab({super.key});

  @override
  State<ClassTab> createState() => _ClassTabState();
}

class _ClassTabState extends State<ClassTab> {
  static const _kinds = {null: 'Tất cả', 'Question': 'Câu hỏi', 'Discussion': 'Thảo luận', 'Announcement': 'Thông báo'};
  final _service = ClassSpaceService();
  final _search = TextEditingController();
  String? _kind;
  ClassSpace? _space;
  List<ThreadSummary> _threads = [];
  bool _hasMore = false;
  int _page = 1;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _load({bool more = false}) async {
    setState(() {
      _loading = !more && _space == null;
      _error = null;
    });
    try {
      final space = await _service.threads(kind: _kind, search: _search.text.trim(), page: more ? _page + 1 : 1);
      setState(() {
        _space = space;
        _threads = more ? [..._threads, ...space.threads.items] : space.threads.items;
        _page = space.threads.pageIndex;
        _hasMore = space.threads.hasNextPage;
      });
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _openThread(int id) async {
    await Navigator.push(context, MaterialPageRoute(builder: (_) => ThreadDetailScreen(threadId: id)));
    if (mounted) _load();
  }

  Future<void> _create() async {
    final id = await Navigator.push<int>(context, MaterialPageRoute(builder: (_) => ThreadFormScreen(isMonitor: _space?.isMonitor ?? false)));
    if (!mounted) return;
    await _load();
    if (id != null) await _openThread(id);
  }

  @override
  Widget build(BuildContext context) {
    final space = _space;
    if (_loading) return const Padding(padding: EdgeInsets.all(16), child: SkeletonList(height: 90));
    // A student who is not in a class yet gets 409 from the API: show it as an empty state.
    if (_error != null && space == null) {
      return Padding(padding: const EdgeInsets.all(16), child: ErrorView(message: _error!, onRetry: _load));
    }

    return Scaffold(
      backgroundColor: Colors.transparent,
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _create,
        icon: const Icon(LucideIcons.plus),
        label: const Text('Đăng bài'),
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 88),
          children: [
            if (space != null) ...[
              Text('Lớp ${space.classCode} · ${space.className}', style: Theme.of(context).textTheme.titleMedium),
              Text('Lớp trưởng: ${space.monitorName ?? 'chưa chỉ định'}. Chỉ thành viên trong lớp mới xem được.', style: const TextStyle(color: AppColors.muted, fontSize: 13)),
              if (space.isMonitor)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: OutlinedButton.icon(
                    style: OutlinedButton.styleFrom(foregroundColor: AppColors.charcoal, side: const BorderSide(color: AppColors.accent, width: 1.5)),
                    onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const EscalationsScreen())).then((_) => _load()),
                    icon: const Icon(LucideIcons.shieldQuestion, size: 18),
                    label: const Text('Câu hỏi đã chuyển P.QL HSSV'),
                  ),
                ),
            ],
            const SizedBox(height: 12),
            TextField(
              controller: _search,
              textInputAction: TextInputAction.search,
              onSubmitted: (_) => _load(),
              decoration: const InputDecoration(hintText: 'Tìm theo tiêu đề, nội dung', prefixIcon: Icon(LucideIcons.search, size: 18)),
            ),
            const SizedBox(height: 8),
            Wrap(spacing: 8, children: [
              for (final entry in _kinds.entries)
                ChoiceChip(
                  label: Text(entry.value),
                  selected: _kind == entry.key,
                  onSelected: (_) {
                    setState(() => _kind = entry.key);
                    _load();
                  },
                ),
            ]),
            const SizedBox(height: 12),
            if (_error != null) Padding(padding: const EdgeInsets.only(bottom: 12), child: ErrorView(message: _error!, onRetry: _load)),
            if (_threads.isEmpty)
              EmptyState(
                icon: LucideIcons.messagesSquare,
                message: _search.text.isEmpty && _kind == null ? 'Lớp chưa có chủ đề nào. Hãy mở đầu bằng một câu hỏi!' : 'Không có chủ đề phù hợp.',
                actionLabel: 'Đăng bài',
                onAction: _create,
              )
            else ...[
              for (var i = 0; i < _threads.length; i++) EntryAnimation(index: i, child: _ThreadCard(thread: _threads[i], onTap: () => _openThread(_threads[i].id))),
              if (_hasMore) OutlinedButton(onPressed: () => _load(more: true), child: const Text('Xem thêm')),
            ],
          ],
        ),
      ),
    );
  }
}

class _ThreadCard extends StatelessWidget {
  const _ThreadCard({required this.thread, required this.onTap});
  final ThreadSummary thread;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final escalation = escalationLabel(thread.escalationStatus);
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Wrap(spacing: 6, runSpacing: 4, children: [
                if (thread.isPinned) const StatusBadge('Pinned', label: 'Ghim'),
                StatusBadge(thread.kind),
                if (thread.status != 'Open') StatusBadge(thread.status),
                if (escalation != null) StatusBadge(thread.escalationStatus == 'Answered' ? 'Accepted' : 'Pending', label: escalation),
              ]),
              const SizedBox(height: 6),
              Text(thread.title, style: const TextStyle(fontWeight: FontWeight.w700)),
              Text(thread.preview, maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(color: AppColors.muted, fontSize: 13)),
              const SizedBox(height: 4),
              Text(
                '${thread.author.fullName}${thread.author.isMonitor ? ' (lớp trưởng)' : ''} · ${thread.replyCount} trả lời · ${VnTime.dateTime(thread.lastActivityAt)}',
                style: const TextStyle(fontSize: 12, color: AppColors.muted),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
