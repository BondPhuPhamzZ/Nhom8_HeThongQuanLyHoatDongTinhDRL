import 'package:flutter_test/flutter_test.dart';
import 'package:nhom8_qlhd_tinh_drl/models/activity.dart';
import 'package:nhom8_qlhd_tinh_drl/models/assistant.dart';
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

  test('CheckInStatus parses the participation payload', () {
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
  });

  test('ThreadDetail carries the monitor escalation with the official answer', () {
    Map<String, dynamic> thread(Object? escalation) => {
          'id': 3, 'kind': 'Question', 'title': 'Lịch đăng kí môn', 'preview': 'Hỏi', 'status': 'Open', 'isPinned': false,
          'author': {'id': 2, 'studentCode': 'SV210001', 'fullName': 'Lớp trưởng', 'isMonitor': true, 'isMe': true},
          'replyCount': 0, 'createdAt': '2026-11-11T12:48:00Z', 'lastActivityAt': '2026-11-11T12:48:00Z',
          'escalationStatus': 'Answered', 'body': 'Hỏi', 'replies': [], 'escalation': escalation,
        };
    final answered = ThreadDetail.fromJson(thread({
      'id': 7, 'threadId': 3, 'question': 'Hỏi lịch', 'status': 'Answered', 'createdAt': '2026-11-11T12:48:00Z',
      'answer': 'Xem thông báo Phòng Đào tạo', 'answeredAt': '2026-11-11T13:00:00Z', 'canRelay': true,
    }));
    expect(answered.escalation?.answer, 'Xem thông báo Phòng Đào tạo');
    expect(answered.escalation?.canRelay, isTrue);
    expect(ThreadDetail.fromJson(thread(null)).escalation, isNull);
  });

  test('History items and appeals carry the US-37 appeal fields', () {
    final item = ActivityHistoryItem.fromJson({
      'activityId': 4, 'activityName': 'Hội thảo', 'organizerName': 'VNG', 'registrationStatus': 'Attended',
      'registeredAt': '2026-11-10T01:00:00Z', 'trainingPoints': 3, 'pointStatus': 'Rejected', 'pointRecordId': 9,
      'appealStatus': null, 'canAppeal': true, 'checkInCount': 1, 'sessions': [],
    });
    expect(item.canAppeal, isTrue);
    expect(item.pointRecordId, 9);
    expect(ActivityHistoryItem.fromJson({'activityId': 1, 'registeredAt': '2026-11-10T01:00:00Z'}).canAppeal, isFalse);

    final appeal = PointAppeal.fromJson({
      'id': 2, 'pointRecordId': 9, 'activityId': 4, 'activityName': 'Hội thảo', 'points': 3, 'reason': 'Em có mặt cả buổi',
      'status': 'Pending', 'staffResponse': null, 'createdAt': '2026-11-11T02:00:00Z', 'resolvedAt': null,
    });
    expect(appeal.isPending, isTrue);
    expect(appeal.resolvedAt, isNull);
    expect(PointAppeal.labelOf('Accepted'), 'Được chấp nhận');
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

  group('Assistant (US-31)', () {
    test('parses the chat answer with its verified activity cards', () {
      final reply = AssistantReply.fromJson({
        'reply': 'Bạn còn thiếu 30 điểm.',
        'activities': [
          {
            'id': 12,
            'activityName': 'Hiến máu',
            'trainingPoints': 15,
            'campus': 'Cơ sở 1',
            'startTime': '2026-11-10T01:00:00Z',
            'registrationCloseTime': '2026-11-09T00:00:00Z',
            'isRegistrationOpen': true,
            'myRegistrationStatus': null,
          },
        ],
        'toolsUsed': ['get_points_summary', 'search_activities'],
        'droppedActivityReferences': 1,
        'model': 'gemini-3.5-flash-lite',
      });

      expect(reply.reply, 'Bạn còn thiếu 30 điểm.');
      expect(reply.droppedActivityReferences, 1);
      final activity = reply.activities.single;
      expect(activity.id, 12);
      expect(activity.trainingPoints, 15);
      expect(VnTime.dateTime(activity.startTime), '08:00 10/11/2026');
      expect(activity.isRegistrationOpen, isTrue);
      expect(ChatMessage.assistant(reply).toolLabels, ['điểm và mục tiêu của kỳ', 'hoạt động phù hợp']);
    });

    test('reads the status and sends only answered turns back', () {
      expect(AssistantStatus.fromJson({'enabled': true, 'model': 'gemini-3.5-flash-lite'}).enabled, isTrue);
      expect(AssistantStatus.fromJson({}).enabled, isFalse);

      final question = ChatMessage.user('Mình còn thiếu bao nhiêu điểm?');
      expect(question.toJson(), {'role': 'user', 'content': 'Mình còn thiếu bao nhiêu điểm?'});
      expect(question.inHistory, isTrue);
      expect(question.withoutHistory().inHistory, isFalse);
      final error = ChatMessage.error('Trợ lý đang quá tải.');
      expect(error.isError, isTrue);
      expect(error.inHistory, isFalse);
      expect(const ChatMessage(role: ChatRole.assistant, text: 'Chào bạn').toJson()['role'], 'assistant');
    });

    test('sends the API signature back with assistant answers only (US-32)', () {
      final reply = AssistantReply.fromJson({
        'reply': 'Bạn còn thiếu 50 điểm.',
        'replySignature': 'sig-abc',
        'personalDataRedacted': true,
        'remainingToday': 38,
      });
      final answer = ChatMessage.assistant(reply);
      expect(answer.toJson(), {'role': 'assistant', 'content': 'Bạn còn thiếu 50 điểm.', 'signature': 'sig-abc'});
      expect(answer.personalDataRedacted, isTrue);
      expect(reply.remainingToday, 38);
      expect(ChatMessage.user('Xin chào').toJson().containsKey('signature'), isFalse);
      expect(AssistantStatus.fromJson({'enabled': true, 'dailyQuestionLimit': 40, 'remainingToday': 12}).remainingToday, 12);
    });
  });
}
