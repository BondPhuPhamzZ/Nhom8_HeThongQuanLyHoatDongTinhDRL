import 'package:flutter/material.dart';
import '../core/theme/app_theme.dart';
import '../features/dashboard/presentation/dashboard_page.dart';

class Nhom8App extends StatelessWidget {
  const Nhom8App({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Hoạt động & điểm rèn luyện',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      home: const DashboardPage(),
    );
  }
}
