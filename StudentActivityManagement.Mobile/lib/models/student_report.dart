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
