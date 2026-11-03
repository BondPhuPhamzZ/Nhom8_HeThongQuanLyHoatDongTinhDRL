import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:provider/provider.dart';

import '../../constants/app_colors.dart';
import '../../models/app_notification.dart';
import '../../providers/notification_provider.dart';
import '../../services/api_client.dart';
import '../../services/notification_service.dart';
import '../../utils/vn_time.dart';
import '../activity/activity_detail_screen.dart';
import '../widgets/feedback_views.dart';

/// US-06/US-12: reminders before registered or required activities and new activities for the class.
class NotificationsTab extends StatefulWidget {
  const NotificationsTab({super.key});

  @override
  State<NotificationsTab> createState() => _NotificationsTabState();
}

class _NotificationsTabState extends State<NotificationsTab> {
  final _service = NotificationService();
  bool _unreadOnly = false;
  List<AppNotification> _items = [];
  int _page = 1;
  bool _hasMore = false;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load({bool more = false}) async {
    setState(() {
      _loading = !more && _items.isEmpty;
      _error = null;
    });
    try {
      final page = await _service.list(unreadOnly: _unreadOnly, page: more ? _page + 1 : 1);
      setState(() {
        _items = more ? [..._items, ...page.items] : page.items;
        _page = page.pageIndex;
        _hasMore = page.hasNextPage;
      });
      if (mounted) context.read<NotificationProvider>().refresh();
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _open(AppNotification item) async {
    if (item.isUnread) {
      try {
        await _service.markRead(item.id);
      } on ApiException {
        // Opening the activity matters more than the read mark.
      }
    }
    if (!mounted) return;
    if (item.activityId > 0) {
      await Navigator.push(context, MaterialPageRoute(builder: (_) => ActivityDetailScreen(activityId: item.activityId)));
    }
    if (mounted) _load();
  }

  @override
  Widget build(BuildContext context) => RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Row(children: [
              Expanded(child: Text('Nhắc lịch và hoạt động mới dành cho bạn.', style: Theme.of(context).textTheme.bodySmall)),
              FilterChip(
                label: const Text('Chưa đọc'),
                selected: _unreadOnly,
                onSelected: (value) {
                  setState(() {
                    _unreadOnly = value;
                    _items = [];
                  });
                  _load();
                },
              ),
            ]),
            const SizedBox(height: 12),
            if (_loading)
              const SkeletonList(height: 84)
            else if (_error != null)
              ErrorView(message: _error!, onRetry: _load)
            else if (_items.isEmpty)
              EmptyState(icon: LucideIcons.bell, message: _unreadOnly ? 'Không có thông báo chưa đọc.' : 'Chưa có thông báo nào.')
            else ...[
              for (var i = 0; i < _items.length; i++)
                EntryAnimation(
                  index: i,
                  child: Card(
                    child: ListTile(
                      onTap: () => _open(_items[i]),
                      leading: Icon(
                        _items[i].kind == 'ActivityReminder' ? LucideIcons.bellRing : LucideIcons.megaphone,
                        color: _items[i].isUnread ? AppColors.primary : AppColors.muted,
                      ),
                      title: Text(_items[i].title, style: TextStyle(fontWeight: _items[i].isUnread ? FontWeight.w700 : FontWeight.w400)),
                      subtitle: Text('${_items[i].kindLabel} · ${VnTime.dateTime(_items[i].createdAt)}\n${_items[i].body}', maxLines: 3, overflow: TextOverflow.ellipsis),
                      isThreeLine: true,
                      trailing: _items[i].isUnread
                          ? Container(width: 8, height: 8, decoration: const BoxDecoration(color: AppColors.accent, shape: BoxShape.circle))
                          : null,
                    ),
                  ),
                ),
              if (_hasMore) OutlinedButton(onPressed: () => _load(more: true), child: const Text('Xem thêm')),
            ],
          ],
        ),
      );
}
