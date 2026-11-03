import 'package:flutter/material.dart';
import '../../../core/config/api_config.dart';
import '../../../core/theme/app_theme.dart';

class DashboardPage extends StatelessWidget {
  const DashboardPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Hoạt động & điểm rèn luyện')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: ListView(
              padding: const EdgeInsets.all(24),
              children: [
                Text('Ứng dụng Nhóm 8', style: Theme.of(context).textTheme.headlineMedium),
                const SizedBox(height: 8),
                const Text('Flutter sẽ dùng REST API hiện có cho sinh viên và lớp trưởng.'),
                const SizedBox(height: 24),
                const _FeatureCard(title: 'Hoạt động', detail: 'Danh sách, chi tiết và đăng ký', icon: Icons.event_available_outlined),
                const _FeatureCard(title: 'Tham gia', detail: 'Điểm danh và minh chứng', icon: Icons.fact_check_outlined),
                const _FeatureCard(title: 'Điểm rèn luyện', detail: 'Lịch sử và gợi ý bổ sung điểm', icon: Icons.insights_outlined),
                const _FeatureCard(title: 'Lớp của tôi', detail: 'Thông báo và trao đổi', icon: Icons.forum_outlined),
                const SizedBox(height: 12),
                Text(
                  ApiConfig.isConfigured ? 'Đã khai báo địa chỉ API cho bản chạy này.' : 'Chưa cấu hình API_BASE_URL; các luồng nghiệp vụ chưa được kết nối.',
                  style: TextStyle(color: ApiConfig.isConfigured ? AppTheme.success : AppTheme.charcoal),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _FeatureCard extends StatelessWidget {
  const _FeatureCard({required this.title, required this.detail, required this.icon});
  final String title;
  final String detail;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Card(
    child: ListTile(
      leading: Icon(icon, color: AppTheme.primary),
      title: Text(title),
      subtitle: Text(detail),
    ),
  );
}
