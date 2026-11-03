import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:provider/provider.dart';

import '../providers/auth_provider.dart';
import 'auth/splash_screen.dart';
import 'tabs/profile_tab.dart';

/// Signed-in shell: one bottom navigation tab per student flow.
class MainPage extends StatefulWidget {
  const MainPage({super.key});

  @override
  State<MainPage> createState() => _MainPageState();
}

class _MainPageState extends State<MainPage> {
  int _index = 0;

  static final _tabs = <({String title, IconData icon, Widget page})>[
    (title: 'Tài khoản', icon: LucideIcons.user, page: const ProfileTab()),
  ];

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
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
      bottomNavigationBar: _tabs.length < 2
          ? null
          : NavigationBar(
              selectedIndex: _index,
              onDestinationSelected: (index) => setState(() => _index = index),
              destinations: [for (final item in _tabs) NavigationDestination(icon: Icon(item.icon), label: item.title)],
            ),
    );
  }
}
