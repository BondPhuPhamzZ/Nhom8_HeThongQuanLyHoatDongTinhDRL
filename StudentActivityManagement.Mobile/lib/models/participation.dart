import '../utils/vn_time.dart';

/// `StudentCheckInStatusDto`: check-in method and window for each session of an activity (US-07).
class CheckInStatus {
  CheckInStatus({required this.activityStatus, required this.method, required this.registrationStatus, required this.sessions});

  final String activityStatus;

  /// Code (the student enters the session code) or Manual (staff record attendance).
  final String method;
  final String registrationStatus;
  final List<SessionCheckIn> sessions;

  bool get usesCode => method == 'Code';

  factory CheckInStatus.fromJson(Map<String, dynamic> json) => CheckInStatus(
        activityStatus: json['activityStatus'] as String? ?? '',
        method: json['method'] as String? ?? '',
        registrationStatus: json['registrationStatus'] as String? ?? '',
        sessions: ((json['sessions'] as List?) ?? const [])
            .map((raw) => SessionCheckIn.fromJson((raw as Map).cast<String, dynamic>()))
            .toList(),
      );
}

class SessionCheckIn {
  SessionCheckIn({
    required this.sessionId,
    required this.sessionName,
    required this.startTime,
    required this.endTime,
    required this.opensAt,
    required this.closesAt,
    required this.canCheckIn,
    required this.checkedInAt,
  });

  final int sessionId;
  final String sessionName;
  final DateTime startTime;
  final DateTime endTime;
  final DateTime opensAt;
  final DateTime closesAt;
  final bool canCheckIn;
  final DateTime? checkedInAt;

  factory SessionCheckIn.fromJson(Map<String, dynamic> json) => SessionCheckIn(
        sessionId: json['sessionId'] as int,
        sessionName: json['sessionName'] as String? ?? '',
        startTime: VnTime.parse(json['startTime']),
        endTime: VnTime.parse(json['endTime']),
        opensAt: VnTime.parse(json['opensAt']),
        closesAt: VnTime.parse(json['closesAt']),
        canCheckIn: json['canCheckIn'] as bool? ?? false,
        checkedInAt: VnTime.parseOrNull(json['checkedInAt']),
      );
}
