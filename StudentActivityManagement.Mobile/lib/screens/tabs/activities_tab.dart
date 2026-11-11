import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../models/activity.dart';
import '../../services/activity_service.dart';
import '../../services/api_client.dart';
import '../activity/activity_detail_screen.dart';
import '../widgets/activity_card.dart';
import '../widgets/feedback_views.dart';

/// US-02: activities open to the student ("Sắp diễn ra") and the student's own registrations ("Của tôi").
class ActivitiesTab extends StatefulWidget {
  const ActivitiesTab({super.key});

  @override
  State<ActivitiesTab> createState() => _ActivitiesTabState();
}

class _ActivitiesTabState extends State<ActivitiesTab> {
  static const _campuses = ['Cơ sở 1', 'Cơ sở 2', 'Cơ sở 3'];
  final _service = ActivityService();
  final _search = TextEditingController();

  bool _mine = false;
  String? _campus;
  List<Activity> _items = [];
  int _page = 1;
  bool _hasMore = false;
  bool _loading = true;
  bool _loadingMore = false;
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

  Future<void> _load() async {
    setState(() {
      _loading = _items.isEmpty;
      _error = null;
    });
    try {
      final page = await _service.list(search: _search.text.trim(), campus: _campus, registered: _mine);
      setState(() {
        _items = page.items;
        _page = 1;
        _hasMore = page.hasNextPage;
      });
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _loadMore() async {
    setState(() => _loadingMore = true);
    try {
      final page = await _service.list(search: _search.text.trim(), campus: _campus, registered: _mine, page: _page + 1);
      setState(() {
        _items = [..._items, ...page.items];
        _page = page.pageIndex;
        _hasMore = page.hasNextPage;
      });
    } on ApiException catch (error) {
      if (mounted) showMessage(context, error.message, error: true);
    } finally {
      if (mounted) setState(() => _loadingMore = false);
    }
  }

  void _selectCampus(String? campus) {
    if (campus == _campus) return;
    setState(() => _campus = campus);
    _load();
  }

  void _switch(bool mine) {
    if (mine == _mine) return;
    setState(() {
      _mine = mine;
      _items = [];
    });
    _load();
  }

  Future<void> _open(Activity activity) async {
    await Navigator.push(context, MaterialPageRoute(builder: (_) => ActivityDetailScreen(activityId: activity.id)));
    if (mounted) _load();
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          SegmentedButton<bool>(
            segments: const [
              ButtonSegment(value: false, label: Text('Sắp diễn ra'), icon: Icon(LucideIcons.calendar, size: 16)),
              ButtonSegment(value: true, label: Text('Của tôi'), icon: Icon(LucideIcons.calendarCheck, size: 16)),
            ],
            selected: {_mine},
            onSelectionChanged: (value) => _switch(value.first),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _search,
            textInputAction: TextInputAction.search,
            onSubmitted: (_) => _load(),
            decoration: InputDecoration(
              hintText: 'Tìm theo tên, đơn vị, địa điểm',
              prefixIcon: const Icon(LucideIcons.search, size: 18),
              suffixIcon: _search.text.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'Xóa tìm kiếm',
                      icon: const Icon(LucideIcons.x, size: 18),
                      onPressed: () {
                        _search.clear();
                        _load();
                      },
                    ),
            ),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            children: [
              ChoiceChip(label: const Text('Mọi cơ sở'), selected: _campus == null, onSelected: (_) => _selectCampus(null)),
              for (final campus in _campuses)
                ChoiceChip(label: Text(campus), selected: _campus == campus, onSelected: (_) => _selectCampus(campus)),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            _mine
                ? 'Hoạt động bạn đã đăng ký, kể cả đã kết thúc: xem check-in và điểm đã ghi.'
                : 'Chỉ hiện hoạt động bạn thuộc đối tượng tham gia, sắp diễn ra hoặc đang diễn ra.',
            style: const TextStyle(color: AppColors.muted, fontSize: 13),
          ),
          const SizedBox(height: 12),
          if (_loading)
            const SkeletonList(height: 150)
          else if (_error != null)
            ErrorView(message: _error!, onRetry: _load)
          else if (_items.isEmpty)
            EmptyState(
              icon: LucideIcons.calendar,
              message: _mine ? 'Bạn chưa đăng ký hoạt động nào.' : 'Chưa có hoạt động phù hợp với bạn.',
              actionLabel: _mine ? 'Xem hoạt động sắp diễn ra' : null,
              onAction: _mine ? () => _switch(false) : null,
            )
          else ...[
            for (var i = 0; i < _items.length; i++)
              EntryAnimation(index: i, child: ActivityCard(activity: _items[i], onTap: () => _open(_items[i]))),
            if (_hasMore)
              BusyButton(label: 'Xem thêm', outlined: true, busy: _loadingMore, onPressed: _loadMore),
          ],
        ],
      ),
    );
  }
}
