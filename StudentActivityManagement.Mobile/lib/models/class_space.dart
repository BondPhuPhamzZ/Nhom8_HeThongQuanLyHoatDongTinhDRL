import '../utils/vn_time.dart';
import 'paged_result.dart';

/// `ClassSpaceDto`: the signed-in student's class and one page of its threads (US-09).
class ClassSpace {
  ClassSpace({required this.classCode, required this.className, required this.monitorName, required this.isMonitor, required this.threads});

  final String classCode;
  final String className;
  final String? monitorName;

  /// The viewer is this class's monitor (announcements, pin, answers, escalations).
  final bool isMonitor;
  final PagedResult<ThreadSummary> threads;

  factory ClassSpace.fromJson(Map<String, dynamic> json) => ClassSpace(
        classCode: json['classCode'] as String? ?? '',
        className: json['className'] as String? ?? '',
        monitorName: json['monitorName'] as String?,
        isMonitor: json['isMonitor'] as bool? ?? false,
        threads: PagedResult.fromJson((json['threads'] as Map).cast<String, dynamic>(), ThreadSummary.fromJson),
      );
}

class PostAuthor {
  PostAuthor({required this.fullName, required this.studentCode, required this.isMonitor, required this.isMe});

  final String fullName;
  final String studentCode;
  final bool isMonitor;
  final bool isMe;

  factory PostAuthor.fromJson(Map<String, dynamic>? json) => PostAuthor(
        fullName: json?['fullName'] as String? ?? '',
        studentCode: json?['studentCode'] as String? ?? '',
        isMonitor: json?['isMonitor'] as bool? ?? false,
        isMe: json?['isMe'] as bool? ?? false,
      );
}

class ThreadSummary {
  ThreadSummary({
    required this.id,
    required this.kind,
    required this.title,
    required this.preview,
    required this.status,
    required this.isPinned,
    required this.author,
    required this.replyCount,
    required this.createdAt,
    required this.lastActivityAt,
    required this.escalationStatus,
  });

  final int id;
  final String kind;
  final String title;
  final String preview;
  final String status;
  final bool isPinned;
  final PostAuthor author;
  final int replyCount;
  final DateTime createdAt;
  final DateTime lastActivityAt;

  /// Latest escalation to Phòng QL HSSV (US-15): Pending, Answered, Withdrawn or null.
  final String? escalationStatus;

  factory ThreadSummary.fromJson(Map<String, dynamic> json) => ThreadSummary(
        id: json['id'] as int,
        kind: json['kind'] as String? ?? '',
        title: json['title'] as String? ?? '',
        preview: json['preview'] as String? ?? '',
        status: json['status'] as String? ?? '',
        isPinned: json['isPinned'] as bool? ?? false,
        author: PostAuthor.fromJson((json['author'] as Map?)?.cast<String, dynamic>()),
        replyCount: json['replyCount'] as int? ?? 0,
        createdAt: VnTime.parse(json['createdAt']),
        lastActivityAt: VnTime.parse(json['lastActivityAt']),
        escalationStatus: json['escalationStatus'] as String?,
      );
}

class ThreadDetail {
  ThreadDetail({
    required this.summary,
    required this.body,
    required this.canDelete,
    required this.canEscalate,
    required this.canPin,
    required this.canManageAnswers,
    required this.replies,
    this.escalation,
  });

  final ThreadSummary summary;
  final String body;
  final bool canDelete;
  final bool canEscalate;
  final bool canPin;
  final bool canManageAnswers;
  final List<ThreadReply> replies;

  /// Monitor only: the latest question raised to Phòng QL HSSV, with its official answer once given.
  final Escalation? escalation;

  factory ThreadDetail.fromJson(Map<String, dynamic> json) => ThreadDetail(
        summary: ThreadSummary.fromJson(json),
        body: json['body'] as String? ?? '',
        canDelete: json['canDelete'] as bool? ?? false,
        canEscalate: json['canEscalate'] as bool? ?? false,
        canPin: json['canPin'] as bool? ?? false,
        canManageAnswers: json['canManageAnswers'] as bool? ?? false,
        replies: ((json['replies'] as List?) ?? const [])
            .map((raw) => ThreadReply.fromJson((raw as Map).cast<String, dynamic>()))
            .toList(),
        escalation: json['escalation'] is Map ? Escalation.fromJson((json['escalation'] as Map).cast<String, dynamic>()) : null,
      );
}

class ThreadReply {
  ThreadReply({
    required this.id,
    required this.body,
    required this.author,
    required this.isAnswer,
    required this.createdAt,
    required this.canDelete,
    required this.canMarkAnswer,
  });

  final int id;
  final String body;
  final PostAuthor author;
  final bool isAnswer;
  final DateTime createdAt;
  final bool canDelete;
  final bool canMarkAnswer;

  factory ThreadReply.fromJson(Map<String, dynamic> json) => ThreadReply(
        id: json['id'] as int,
        body: json['body'] as String? ?? '',
        author: PostAuthor.fromJson((json['author'] as Map?)?.cast<String, dynamic>()),
        isAnswer: json['isAnswer'] as bool? ?? false,
        createdAt: VnTime.parse(json['createdAt']),
        canDelete: json['canDelete'] as bool? ?? false,
        canMarkAnswer: json['canMarkAnswer'] as bool? ?? false,
      );
}

/// `ThreadEscalationDto`: a question the monitor raised to Phòng QL HSSV (US-15/16/20).
class Escalation {
  Escalation({
    required this.id,
    required this.threadId,
    required this.threadTitle,
    required this.question,
    required this.status,
    required this.raisedByName,
    required this.createdAt,
    required this.answer,
    required this.answeredByName,
    required this.answeredAt,
    required this.relayedByName,
    required this.relayedAt,
    required this.canRelay,
  });

  final int id;
  final int threadId;
  final String threadTitle;
  final String question;
  final String status;
  final String? raisedByName;
  final DateTime createdAt;
  final String? answer;
  final String? answeredByName;
  final DateTime? answeredAt;
  final String? relayedByName;
  final DateTime? relayedAt;
  final bool canRelay;

  factory Escalation.fromJson(Map<String, dynamic> json) => Escalation(
        id: json['id'] as int,
        threadId: json['threadId'] as int? ?? 0,
        threadTitle: json['threadTitle'] as String? ?? '',
        question: json['question'] as String? ?? '',
        status: json['status'] as String? ?? '',
        raisedByName: json['raisedByName'] as String?,
        createdAt: VnTime.parse(json['createdAt']),
        answer: json['answer'] as String?,
        answeredByName: json['answeredByName'] as String?,
        answeredAt: VnTime.parseOrNull(json['answeredAt']),
        relayedByName: json['relayedByName'] as String?,
        relayedAt: VnTime.parseOrNull(json['relayedAt']),
        canRelay: json['canRelay'] as bool? ?? false,
      );
}

/// Labels of the escalation state shown on threads, as on the Web.
String? escalationLabel(String? status) => switch (status) {
      'Pending' => 'Đã chuyển P.QL HSSV',
      'Answered' => 'P.QL HSSV đã trả lời',
      'Withdrawn' => 'Đã rút lại',
      _ => null,
    };
