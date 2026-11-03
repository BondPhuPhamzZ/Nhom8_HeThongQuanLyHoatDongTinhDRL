import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:provider/provider.dart';

import '../constants/app_colors.dart';
import '../providers/auth_provider.dart';
import '../providers/notification_provider.dart';
import 'auth/splash_screen.dart';
import 'tabs/activities_tab.dart';
import 'tabs/notifications_tab.dart';
import 'tabs/points_tab.dart';
import 'tabs/profile_tab.dart';

/// Signed-in shell: one bottom navigation tab per student flow.
class MainPage extends StatefulWidget {
  const MainPage({super.key});

  @override
  State<MainPage> createState() => _MainPageState();
}

class _MainPageState extends State<MainPage> {
  int _index = 0;

  static final _tabs = <({String title, IconData icon, Widget page, bool badge})>[
    (title: 'Hoạt động', icon: LucideIcons.calendar, page: const ActivitiesTab(), badge: false),
    (title: 'Điểm', icon: LucideIcons.chartColumn, page: const PointsTab(), badge: false),
    (title: 'Thông báo', icon: LucideIcons.bell, page: const NotificationsTab(), badge: true),
    (title: 'Tài khoản', icon: LucideIcons.user, page: const ProfileTab(), badge: false),
  ];

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => context.read<NotificationProvider>().refresh());
  }

  void _select(int index) {
    setState(() => _index = index);
    context.read<NotificationProvider>().refresh();
  }

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final unread = context.watch<NotificationProvider>().unread;
    final tab = _tabs[_index];
    return Scaffold(
      appBar: AppBar(
        titleSpacing: 16,
        title: Row(
          children: [
            const BrandMark(size: 28),
            const SizedBox(width: 10),
            Expanded(child: Text(tab.title, overflow: TextOverflow.ellipsis)),
          ],
        ),
        actions: [
          if (user != null)
            Padding(
              padding: const EdgeInsets.only(right: 16),
              child: Center(child: Text(user.classCode ?? user.roleLabel, style: const TextStyle(fontSize: 13))),
            ),
        ],
      ),
      body: IndexedStack(index: _index, children: [for (final item in _tabs) item.page]),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: _select,
        destinations: [
          for (final item in _tabs)
            NavigationDestination(
              icon: item.badge && unread > 0
                  ? Badge(label: Text('$unread'), backgroundColor: AppColors.danger, child: Icon(item.icon))
                  : Icon(item.icon),
              label: item.title,
            ),
        ],
      ),
    );
  }
}
