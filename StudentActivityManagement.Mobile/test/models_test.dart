import 'package:flutter_test/flutter_test.dart';
import 'package:nhom8_qlhd_tinh_drl/models/activity.dart';
import 'package:nhom8_qlhd_tinh_drl/models/class_space.dart';
import 'package:nhom8_qlhd_tinh_drl/models/participation.dart';
import 'package:nhom8_qlhd_tinh_drl/models/student_report.dart';
import 'package:nhom8_qlhd_tinh_drl/models/user.dart';
import 'package:nhom8_qlhd_tinh_drl/utils/vn_time.dart';

/// Payloads copied from the API's camelCase JSON, so a renamed DTO field breaks these tests first.
void main() {
  Map<String, dynamic> activityJson({String status = 'Published', String? myStatus, int current = 3}) => {
        'id': 7,
        'activityName': 'Hiến máu',
        'description': 'Mô tả',
        'requirements': '',
        'organizerName': 'Đoàn trường',
        'location': 'Hội trường',
        'campus': 'Cơ sở 1',
        'startTime': '2026-11-10T01:00:00Z',
        'endTime': '2026-11-10T05:00:00Z',
        'registrationOpenTime': '2026-11-01T00:00:00Z',
        'registrationCloseTime': '2026-11-09T00:00:00Z',
        'maxParticipants': 5,
        'currentParticipantsCount': current,
        'trainingPoints': 15,
        'targetAudience': 'Tất cả',
        'status': status,
        'sessions': [
          {'id': 2, 'sessionName': 'Buổi 2', 'campus': 'Cơ sở 1', 'location': 'A', 'startTime': '2026-11-11T01:00:00Z', 'endTime': '2026-11-11T02:00:00Z', 'sortOrder': 2},
          {'id': 1, 'sessionName': 'Buổi 1', 'campus': 'Cơ sở 1', 'location': 'A', 'startTime': '2026-11-10T01:00:00Z', 'endTime': '2026-11-10T02:00:00Z', 'sortOrder': 1},
        ],
        'eligibility': {'isEligible': true, 'participationType': 'Recommended', 'reason': ''},
        'myRegistrationStatus': myStatus,
      };

  group('Activity', () {
    final inWindow = DateTime.utc(2026, 11, 5);

    test('parses the API payload and orders sessions', () {
      final activity = Activity.fromJson(activityJson());
      expect(activity.name, 'Hiến máu');
      expect(activity.remainingSlots, 2);
      expect(activity.orderedSessions.map((session) => session.name), ['Buổi 1', 'Buổi 2']);
      expect(activity.eligibility?.participationType, 'Recommended');
      expect(activity.startTime.isUtc, isTrue);
    });

    test('registration follows the Web rule: published, in window, free place, no place held', () {
      expect(Activity.fromJson(activityJson()).canRegisterAt(inWindow), isTrue);
      expect(Activity.fromJson(activityJson(current: 5)).canRegisterAt(inWindow), isFalse);
      expect(Activity.fromJson(activityJson(status: 'Ongoing')).canRegisterAt(inWindow), isFalse);
      expect(Activity.fromJson(activityJson(myStatus: 'Registered')).canRegisterAt(inWindow), isFalse);
      expect(Activity.fromJson(activityJson()).canRegisterAt(DateTime.utc(2026, 11, 9, 1)), isFalse);
    });

    test('only a Registered place can be cancelled, before registration closes', () {
      expect(Activity.fromJson(activityJson(myStatus: 'Registered')).canCancelAt(inWindow), isTrue);
      expect(Activity.fromJson(activityJson(myStatus: 'Attended')).canCancelAt(inWindow), isFalse);
      expect(Activity.fromJson(activityJson(myStatus: 'Registered')).canCancelAt(DateTime.utc(2026, 11, 9, 1)), isFalse);
    });
  });

  test('AppUser keeps staff out of the student app', () {
    Map<String, dynamic> user(String role) => {'id': 1, 'studentCode': 'X', 'fullName': 'A', 'email': 'a@b', 'phone': '', 'role': role, 'campus': '', 'academicYear': ''};
    expect(AppUser.fromJson(user('Student')).isStudentRole, isTrue);
    expect(AppUser.fromJson(user('Monitor')).isMonitor, isTrue);
    expect(AppUser.fromJson(user('HSSV')).isStudentRole, isFalse);
    expect(AppUser.fromJson(user('SystemAdmin')).isStudentRole, isFalse);
  });

  test('CheckInStatus and Evidence parse the participation payloads', () {
    final status = CheckInStatus.fromJson({
      'activityStatus': 'Ongoing',
      'method': 'Code',
      'registrationStatus': 'Registered',
      'sessions': [
        {'sessionId': 3, 'sessionName': 'Buổi 1', 'startTime': '2026-11-10T01:00:00Z', 'endTime': '2026-11-10T02:00:00Z', 'opensAt': '2026-11-10T00:45:00Z', 'closesAt': '2026-11-10T02:30:00Z', 'canCheckIn': true, 'checkedInAt': null},
      ],
    });
    expect(status.usesCode, isTrue);
    expect(status.sessions.single.canCheckIn, isTrue);
    expect(status.sessions.single.checkedInAt, isNull);

    final evidence = Evidence.fromJson({'id': 1, 'evidenceType': 'ConfirmationCode', 'confirmationCode': 'XN-1', 'reviewStatus': 'Pending', 'uploadedAt': '2026-11-10T03:00:00Z', 'canDelete': true});
    expect(evidence.typeLabel, 'Mã xác nhận');
    expect(evidence.canDelete, isTrue);
  });

  test('PointsSummary reports the gap to the period target', () {
    final summary = PointsSummary.fromJson({
      'period': {'id': 1, 'name': 'HK1', 'startDate': '2026-09-01T00:00:00Z', 'endDate': '2027-01-15T00:00:00Z', 'targetPoints': 10, 'status': 'Open'},
      'approvedPoints': 6,
      'missingPoints': 4,
      'isAtRisk': true,
      'recommendedActivities': [activityJson()],
    });
    expect(summary.progress, closeTo(0.6, 0.0001));
    expect(summary.recommended.single.id, 7);
  });

  test('ClassSpace parses threads and escalation labels', () {
    final space = ClassSpace.fromJson({
      'classId': 1,
      'classCode': '21DTH1',
      'className': 'CNTT 1',
      'monitorName': 'Lớp trưởng',
      'isMonitor': true,
      'threads': {
        'items': [
          {'id': 5, 'kind': 'Question', 'title': 'Hỏi', 'preview': '...', 'status': 'Open', 'isPinned': false, 'author': {'fullName': 'B', 'studentCode': 'SV2', 'isMonitor': false, 'isMe': false}, 'replyCount': 2, 'createdAt': '2026-11-03T07:00:00Z', 'lastActivityAt': '2026-11-03T08:00:00Z', 'escalationStatus': 'Pending'},
        ],
        'totalCount': 1,
        'pageIndex': 1,
        'pageSize': 15,
      },
    });
    expect(space.isMonitor, isTrue);
    expect(space.threads.hasNextPage, isFalse);
    expect(escalationLabel(space.threads.items.single.escalationStatus), 'Đã chuyển P.QL HSSV');
    expect(escalationLabel(null), isNull);
  });

  group('VnTime', () {
    test('shows UTC from the API in Vietnam time', () {
      expect(VnTime.dateTime(DateTime.utc(2026, 11, 3, 17, 5)), '00:05 04/11/2026');
      expect(VnTime.date(DateTime.utc(2026, 11, 3, 16, 59)), '03/11/2026');
    });

    test('treats offset-less timestamps as UTC', () {
      expect(VnTime.parse('2026-11-03T07:00:00'), DateTime.utc(2026, 11, 3, 7));
      expect(VnTime.parse('2026-11-03T07:00:00Z'), DateTime.utc(2026, 11, 3, 7));
      expect(VnTime.parseOrNull(null), isNull);
    });
  });
}
