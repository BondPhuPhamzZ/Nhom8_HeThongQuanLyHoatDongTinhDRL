import 'package:flutter/material.dart';

import '../../constants/app_colors.dart';

/// Soft tinted pill with the same labels and colours as the Web's `<status-badge>` tag helper.
class StatusBadge extends StatelessWidget {
  const StatusBadge(this.value, {super.key, this.label});

  final String value;

  /// Overrides the built-in label (for example the escalation wording).
  final String? label;

  // Tints shared with site.css (.text-bg-*): (background, text).
  static const _green = (Color(0xFFE5F6E9), Color(0xFF1D7F35));
  static const _blue = (AppColors.primarySoft, Color(0xFF3F4AB8));
  static const _grey = (Color(0xFFEEF0F3), Color(0xFF4D5260));
  static const _red = (Color(0xFFFDECEF), Color(0xFFB22A3C));
  static const _amber = (Color(0xFFFFF3D6), Color(0xFF8A5D00));
  static const _sky = (Color(0xFFE6F1FB), Color(0xFF1F6AA8));
  static const _dark = (Color(0xFFE9EAEE), Color(0xFF2A2C33));

  static (String, Color, Color) styleOf(String value) {
    final (text, tint) = switch (value) {
      'Draft' => ('Nháp', _grey),
      'Published' => ('Đang mở', _green),
      'Ongoing' => ('Đang diễn ra', _blue),
      'Completed' => ('Đã kết thúc', _dark),
      'Cancelled' => ('Đã hủy', _red),
      'Registered' => ('Đã đăng ký', _green),
      'Attended' => ('Đã tham gia', _blue),
      'Absent' => ('Vắng mặt', _amber),
      'Required' => ('Bắt buộc', _red),
      'Recommended' => ('Khuyến khích', _amber),
      'Optional' => ('Tự chọn', _sky),
      'Pending' => ('Chờ duyệt', _amber),
      'Accepted' => ('Đã duyệt', _green),
      'Rejected' => ('Từ chối', _red),
      'Approved' => ('Đã ghi điểm', _green),
      'Revoked' => ('Đã thu hồi', _dark),
      'Open' => ('Đang mở', _green),
      'Closed' => ('Đã khóa', _grey),
      'Answered' => ('Đã có lời giải', _green),
      'Question' => ('Câu hỏi', _sky),
      'Discussion' => ('Thảo luận', _blue),
      'Announcement' => ('Thông báo', _red),
      'Withdrawn' => ('Đã rút lại', _grey),
      'Pinned' => ('Ghim', _amber),
      _ => (value, (AppColors.surface, AppColors.charcoal)),
    };
    return (text, tint.$1, tint.$2);
  }

  @override
  Widget build(BuildContext context) {
    final (text, background, foreground) = styleOf(value);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
        border: background == AppColors.surface ? Border.all(color: AppColors.border) : null,
      ),
      child: Text(label ?? text, style: TextStyle(color: foreground, fontSize: 11.5, fontWeight: FontWeight.w600)),
    );
  }
}

/// Points pill ("+10 điểm") in the accent tint.
class PointsChip extends StatelessWidget {
  const PointsChip(this.points, {super.key});

  final int points;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
        decoration: BoxDecoration(color: AppColors.primarySoft, borderRadius: BorderRadius.circular(999)),
        child: Text('+$points điểm', style: const TextStyle(color: Color(0xFF3F4AB8), fontSize: 12, fontWeight: FontWeight.w700)),
      );
}
