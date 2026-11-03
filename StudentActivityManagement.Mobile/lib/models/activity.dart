import '../utils/vn_time.dart';

/// An activity as a student sees it (`ActivityResponseDto` from `/api/v1/student-activities`).
class Activity {
  Activity({
    required this.id,
    required this.name,
    required this.description,
    required this.requirements,
    required this.organizerName,
    required this.location,
    required this.campus,
    required this.startTime,
    required this.endTime,
    required this.registrationOpenTime,
    required this.registrationCloseTime,
    required this.maxParticipants,
    required this.currentParticipants,
    required this.trainingPoints,
    required this.targetAudience,
    required this.status,
    required this.sessions,
    required this.eligibility,
    required this.myRegistrationStatus,
  });

  final int id;
  final String name;
  final String description;
  final String requirements;
  final String organizerName;
  final String location;
  final String campus;
  final DateTime startTime;
  final DateTime endTime;
  final DateTime registrationOpenTime;
  final DateTime registrationCloseTime;
  final int maxParticipants;
  final int currentParticipants;
  final int trainingPoints;
  final String targetAudience;
  final String status;
  final List<ActivitySession> sessions;
  final Eligibility? eligibility;

  /// Registered, Attended, Cancelled... or null when the student never registered.
  final String? myRegistrationStatus;

  int get remainingSlots => (maxParticipants - currentParticipants).clamp(0, maxParticipants);
  bool get isRegistered => myRegistrationStatus == 'Registered';
  bool get hasAttended => myRegistrationStatus == 'Attended';
  bool get holdsPlace => isRegistered || hasAttended;

  List<ActivitySession> get orderedSessions => [...sessions]..sort((a, b) => a.sortOrder.compareTo(b.sortOrder));

  /// Same rule as the Web: published, inside the registration window, a free place, not already holding one.
  bool canRegisterAt(DateTime nowUtc) =>
      !holdsPlace &&
      status == 'Published' &&
      !nowUtc.isBefore(registrationOpenTime) &&
      !nowUtc.isAfter(registrationCloseTime) &&
      remainingSlots > 0;

  /// Registered (not yet checked in) and the registration window has not closed.
  bool canCancelAt(DateTime nowUtc) => isRegistered && !nowUtc.isAfter(registrationCloseTime);

  factory Activity.fromJson(Map<String, dynamic> json) => Activity(
        id: json['id'] as int,
        name: json['activityName'] as String? ?? '',
        description: json['description'] as String? ?? '',
        requirements: json['requirements'] as String? ?? '',
        organizerName: json['organizerName'] as String? ?? '',
        location: json['location'] as String? ?? '',
        campus: json['campus'] as String? ?? '',
        startTime: VnTime.parse(json['startTime']),
        endTime: VnTime.parse(json['endTime']),
        registrationOpenTime: VnTime.parse(json['registrationOpenTime']),
        registrationCloseTime: VnTime.parse(json['registrationCloseTime']),
        maxParticipants: json['maxParticipants'] as int? ?? 0,
        currentParticipants: json['currentParticipantsCount'] as int? ?? 0,
        trainingPoints: json['trainingPoints'] as int? ?? 0,
        targetAudience: json['targetAudience'] as String? ?? '',
        status: json['status'] as String? ?? '',
        sessions: ((json['sessions'] as List?) ?? const [])
            .map((raw) => ActivitySession.fromJson((raw as Map).cast<String, dynamic>()))
            .toList(),
        eligibility: json['eligibility'] is Map ? Eligibility.fromJson((json['eligibility'] as Map).cast<String, dynamic>()) : null,
        myRegistrationStatus: json['myRegistrationStatus'] as String?,
      );
}

class ActivitySession {
  ActivitySession({
    required this.id,
    required this.name,
    required this.campus,
    required this.location,
    required this.startTime,
    required this.endTime,
    required this.sortOrder,
  });

  final int id;
  final String name;
  final String campus;
  final String location;
  final DateTime startTime;
  final DateTime endTime;
  final int sortOrder;

  factory ActivitySession.fromJson(Map<String, dynamic> json) => ActivitySession(
        id: json['id'] as int,
        name: json['sessionName'] as String? ?? '',
        campus: json['campus'] as String? ?? '',
        location: json['location'] as String? ?? '',
        startTime: VnTime.parse(json['startTime']),
        endTime: VnTime.parse(json['endTime']),
        sortOrder: json['sortOrder'] as int? ?? 0,
      );
}

/// Whether the student belongs to the activity's audience and how (Required/Recommended/Optional).
class Eligibility {
  Eligibility({required this.isEligible, required this.participationType, required this.reason});

  final bool isEligible;
  final String? participationType;
  final String reason;

  factory Eligibility.fromJson(Map<String, dynamic> json) => Eligibility(
        isEligible: json['isEligible'] as bool? ?? false,
        participationType: json['participationType'] as String?,
        reason: json['reason'] as String? ?? '',
      );
}
