import '../utils/vn_time.dart';
import 'activity.dart';

/// An evaluation period with the student's approved points in it (`StudentEvaluationPeriodDto`).
class EvaluationPeriod {
  EvaluationPeriod({
    required this.id,
    required this.name,
    required this.startDate,
    required this.endDate,
    required this.targetPoints,
    required this.status,
    required this.approvedPoints,
  });

  final int id;
  final String name;
  final DateTime startDate;
  final DateTime endDate;
  final int targetPoints;
  final String status;
  final int approvedPoints;

  factory EvaluationPeriod.fromJson(Map<String, dynamic> json) => EvaluationPeriod(
        id: json['id'] as int,
        name: json['name'] as String? ?? '',
        startDate: VnTime.parse(json['startDate']),
        endDate: VnTime.parse(json['endDate']),
        targetPoints: json['targetPoints'] as int? ?? 0,
        status: json['status'] as String? ?? '',
        approvedPoints: json['approvedPoints'] as int? ?? 0,
      );
}

/// US-10: one registration in the period with check-ins and the point decision.
class ActivityHistoryItem {
  ActivityHistoryItem({
    required this.activityId,
    required this.activityName,
    required this.organizerName,
    required this.registrationStatus,
    required this.registeredAt,
    required this.trainingPoints,
    required this.pointStatus,
    required this.pointsAwarded,
    required this.pointReason,
    this.pointRecordId,
    this.appealStatus,
    this.canAppeal = false,
    required this.checkInCount,
    required this.sessions,
  });

  final int activityId;
  final String activityName;
  final String organizerName;
  final String registrationStatus;
  final DateTime registeredAt;
  final int trainingPoints;
  final String? pointStatus;
  final int? pointsAwarded;
  final String? pointReason;
  /// US-37: the decision to appeal, the latest appeal status and whether the API allows an appeal now.
  final int? pointRecordId;
  final String? appealStatus;
  final bool canAppeal;
  final int checkInCount;
  final List<SessionHistory> sessions;

  factory ActivityHistoryItem.fromJson(Map<String, dynamic> json) => ActivityHistoryItem(
        activityId: json['activityId'] as int,
        activityName: json['activityName'] as String? ?? '',
        organizerName: json['organizerName'] as String? ?? '',
        registrationStatus: json['registrationStatus'] as String? ?? '',
        registeredAt: VnTime.parse(json['registeredAt']),
        trainingPoints: json['trainingPoints'] as int? ?? 0,
        pointStatus: json['pointStatus'] as String?,
        pointsAwarded: json['pointsAwarded'] as int?,
        pointReason: json['pointReason'] as String?,
        pointRecordId: json['pointRecordId'] as int?,
        appealStatus: json['appealStatus'] as String?,
        canAppeal: json['canAppeal'] as bool? ?? false,
        checkInCount: json['checkInCount'] as int? ?? 0,
        sessions: ((json['sessions'] as List?) ?? const [])
            .map((raw) => SessionHistory.fromJson((raw as Map).cast<String, dynamic>()))
            .toList(),
      );
}

class SessionHistory {
  SessionHistory({required this.sessionName, required this.startTime, required this.checkedInAt});

  final String sessionName;
  final DateTime startTime;
  final DateTime? checkedInAt;

  factory SessionHistory.fromJson(Map<String, dynamic> json) => SessionHistory(
        sessionName: json['sessionName'] as String? ?? '',
        startTime: VnTime.parse(json['startTime']),
        checkedInAt: VnTime.parseOrNull(json['checkedInAt']),
      );
}

/// US-11: current points against the period target and activities to close the gap.
class PointsSummary {
  PointsSummary({
    required this.periodName,
    required this.targetPoints,
    required this.approvedPoints,
    required this.missingPoints,
    required this.isAtRisk,
    required this.recommended,
  });

  final String periodName;
  final int targetPoints;
  final int approvedPoints;
  final int missingPoints;
  final bool isAtRisk;
  final List<Activity> recommended;

  double get progress => targetPoints <= 0 ? 1 : (approvedPoints / targetPoints).clamp(0, 1).toDouble();

  factory PointsSummary.fromJson(Map<String, dynamic> json) {
    final period = (json['period'] as Map).cast<String, dynamic>();
    return PointsSummary(
      periodName: period['name'] as String? ?? '',
      targetPoints: period['targetPoints'] as int? ?? 0,
      approvedPoints: json['approvedPoints'] as int? ?? 0,
      missingPoints: json['missingPoints'] as int? ?? 0,
      isAtRisk: json['isAtRisk'] as bool? ?? false,
      recommended: ((json['recommendedActivities'] as List?) ?? const [])
          .map((raw) => Activity.fromJson((raw as Map).cast<String, dynamic>()))
          .toList(),
    );
  }
}

/// US-37 `PointAppealDto`: a student's appeal against a rejected point decision.
class PointAppeal {
  PointAppeal({
    required this.id,
    required this.pointRecordId,
    required this.activityId,
    required this.activityName,
    required this.points,
    required this.reason,
    required this.status,
    required this.staffResponse,
    required this.createdAt,
    required this.resolvedAt,
  });

  final int id;
  final int pointRecordId;
  final int activityId;
  final String activityName;
  final int points;
  final String reason;
  final String status;
  final String? staffResponse;
  final DateTime createdAt;
  final DateTime? resolvedAt;

  bool get isPending => status == 'Pending';

  /// Same wording as the Web's "Khiếu nại điểm" page.
  static String labelOf(String status) => switch (status) {
        'Pending' => 'Đang chờ xử lý',
        'Accepted' => 'Được chấp nhận',
        'Rejected' => 'Bị từ chối',
        'Withdrawn' => 'Đã rút',
        _ => status,
      };

  factory PointAppeal.fromJson(Map<String, dynamic> json) => PointAppeal(
        id: json['id'] as int,
        pointRecordId: json['pointRecordId'] as int? ?? 0,
        activityId: json['activityId'] as int? ?? 0,
        activityName: json['activityName'] as String? ?? '',
        points: json['points'] as int? ?? 0,
        reason: json['reason'] as String? ?? '',
        status: json['status'] as String? ?? '',
        staffResponse: json['staffResponse'] as String?,
        createdAt: VnTime.parse(json['createdAt']),
        resolvedAt: VnTime.parseOrNull(json['resolvedAt']),
      );
}
