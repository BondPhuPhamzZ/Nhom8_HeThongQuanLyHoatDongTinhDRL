import '../utils/vn_time.dart';

/// `UserNotificationDto`: activity reminders (US-06), new activities for the student (US-06) or the class (US-12).
class AppNotification {
  AppNotification({
    required this.id,
    required this.kind,
    required this.activityId,
    required this.title,
    required this.body,
    required this.createdAt,
    required this.readAt,
  });

  final int id;
  final String kind;
  final int activityId;
  final String title;
  final String body;
  final DateTime createdAt;
  final DateTime? readAt;

  bool get isUnread => readAt == null;

  String get kindLabel => switch (kind) {
        'ClassActivity' => 'Hoạt động mới của lớp',
        'StudentActivity' => 'Hoạt động mới',
        'ActivityReminder' => 'Nhắc lịch',
        _ => 'Thông báo',
      };

  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
        id: json['id'] as int,
        kind: json['kind'] as String? ?? '',
        activityId: json['activityId'] as int? ?? 0,
        title: json['title'] as String? ?? '',
        body: json['body'] as String? ?? '',
        createdAt: VnTime.parse(json['createdAt']),
        readAt: VnTime.parseOrNull(json['readAt']),
      );
}
