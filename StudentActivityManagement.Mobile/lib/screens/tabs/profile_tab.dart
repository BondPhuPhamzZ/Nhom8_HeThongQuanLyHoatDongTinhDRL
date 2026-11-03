import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:provider/provider.dart';

import '../../constants/app_colors.dart';
import '../../providers/auth_provider.dart';
import '../../services/preference_service.dart';
import '../widgets/feedback_views.dart';

/// Account information from `/auth/me` and sign-out.
class ProfileTab extends StatelessWidget {
  const ProfileTab({super.key});

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    if (user == null) return const SizedBox.shrink();
    final rows = <(IconData, String, String)>[
      (LucideIcons.hash, 'Mã sinh viên', user.studentCode),
      (LucideIcons.graduationCap, 'Vai trò', user.roleLabel),
      (LucideIcons.users, 'Lớp', user.classCode == null ? 'Chưa xếp lớp' : '${user.classCode} – ${user.className}'),
      (LucideIcons.calendar, 'Khóa', user.academicYear),
      (LucideIcons.mapPin, 'Cơ sở', user.campus),
      (LucideIcons.messageCircle, 'Email', user.email),
      (LucideIcons.server, 'API', PreferenceService.instance.baseUrl),
    ];

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        EntryAnimation(
          child: Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 26,
                    backgroundColor: AppColors.primary,
                    child: Text(user.fullName.isEmpty ? '?' : user.fullName.trim().split(' ').last[0],
                        style: const TextStyle(color: AppColors.white, fontWeight: FontWeight.w700, fontSize: 20)),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(user.fullName, style: Theme.of(context).textTheme.titleMedium),
                        Text('${user.studentCode} · ${user.roleLabel}', style: const TextStyle(color: AppColors.muted)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
        EntryAnimation(
          index: 1,
          child: Card(
            child: Column(
              children: [
                for (final (icon, label, value) in rows)
                  ListTile(
                    dense: true,
                    leading: Icon(icon, size: 18, color: AppColors.muted),
                    title: Text(label, style: const TextStyle(color: AppColors.muted, fontSize: 13)),
                    trailing: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 200),
                      child: Text(value, textAlign: TextAlign.right, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w600)),
                    ),
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger, side: const BorderSide(color: AppColors.danger, width: 1.5)),
          onPressed: () => context.read<AuthProvider>().logout(),
          icon: const Icon(LucideIcons.logOut, size: 18),
          label: const Text('Đăng xuất'),
        ),
      ],
    );
  }
}
