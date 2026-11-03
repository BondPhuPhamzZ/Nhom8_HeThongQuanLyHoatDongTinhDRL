import 'package:flutter/material.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';

/// Flat 4px badge with the same labels and colours as the Web's `<status-badge>` tag helper.
class StatusBadge extends StatelessWidget {
  const StatusBadge(this.value, {super.key, this.label});

  final String value;

  /// Overrides the built-in label (for example the escalation wording).
  final String? label;

  static (String, Color, Color) styleOf(String value) => switch (value) {
        'Draft' => ('Nháp', AppColors.muted, AppColors.white),
        'Published' => ('Đang mở', AppColors.success, AppColors.white),
        'Ongoing' => ('Đang diễn ra', AppColors.primary, AppColors.white),
        'Completed' => ('Đã kết thúc', AppColors.charcoal, AppColors.white),
        'Cancelled' => ('Đã hủy', AppColors.danger, AppColors.white),
        'Registered' => ('Đã đăng ký', AppColors.success, AppColors.white),
        'Attended' => ('Đã tham gia', AppColors.primary, AppColors.white),
        'Absent' => ('Vắng mặt', AppColors.accent, AppColors.charcoal),
        'Required' => ('Bắt buộc', AppColors.danger, AppColors.white),
        'Recommended' => ('Khuyến khích', AppColors.accent, AppColors.charcoal),
        'Optional' => ('Tự chọn', AppColors.info, AppColors.white),
        'Pending' => ('Chờ duyệt', AppColors.accent, AppColors.charcoal),
        'Accepted' => ('Đã duyệt', AppColors.success, AppColors.white),
        'Rejected' => ('Từ chối', AppColors.danger, AppColors.white),
        'Approved' => ('Đã ghi điểm', AppColors.success, AppColors.white),
        'Revoked' => ('Đã thu hồi', AppColors.charcoal, AppColors.white),
        'Open' => ('Đang mở', AppColors.success, AppColors.white),
        'Closed' => ('Đã khóa', AppColors.muted, AppColors.white),
        'Answered' => ('Đã có lời giải', AppColors.success, AppColors.white),
        'Question' => ('Câu hỏi', AppColors.info, AppColors.white),
        'Discussion' => ('Thảo luận', AppColors.primary, AppColors.white),
        'Announcement' => ('Thông báo', AppColors.danger, AppColors.white),
        'Withdrawn' => ('Đã rút lại', AppColors.muted, AppColors.white),
        _ => (value, AppColors.surface, AppColors.charcoal),
      };

  @override
  Widget build(BuildContext context) {
    final (text, background, foreground) = styleOf(value);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(AppStyles.radius),
        border: background == AppColors.surface ? Border.all(color: AppColors.border) : null,
      ),
      child: Text(
        label ?? text,
        style: TextStyle(color: foreground, fontSize: 11, fontWeight: FontWeight.w700, letterSpacing: 0.2),
      ),
    );
  }
}

/// Small pill-free chip for points ("+10 điểm").
class PointsChip extends StatelessWidget {
  const PointsChip(this.points, {super.key});

  final int points;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
        decoration: BoxDecoration(color: const Color(0xFFFFF4D6), borderRadius: BorderRadius.circular(AppStyles.radius)),
        child: Text('+$points điểm', style: const TextStyle(color: Color(0xFF7A5600), fontSize: 12, fontWeight: FontWeight.w700)),
      );
}
