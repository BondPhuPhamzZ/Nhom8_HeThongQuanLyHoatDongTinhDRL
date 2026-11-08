import '../utils/vn_time.dart';

/// `GET /api/v1/assistant/status`: the chat is shown only when the server has the assistant enabled.
class AssistantStatus {
  const AssistantStatus({required this.enabled, this.model, this.dailyQuestionLimit = 0, this.remainingToday = 0});

  final bool enabled;
  final String? model;
  final int dailyQuestionLimit;
  final int remainingToday;

  factory AssistantStatus.fromJson(Map<String, dynamic> json) => AssistantStatus(
        enabled: json['enabled'] as bool? ?? false,
        model: json['model'] as String?,
        dailyQuestionLimit: json['dailyQuestionLimit'] as int? ?? 0,
        remainingToday: json['remainingToday'] as int? ?? 0,
      );
}

/// An activity the answer refers to. The API builds it from tool results, so it exists and the student may open it.
class AssistantActivity {
  const AssistantActivity({
    required this.id,
    required this.name,
    required this.trainingPoints,
    this.campus,
    this.startTime,
    this.registrationCloseTime,
    this.isRegistrationOpen,
    this.myRegistrationStatus,
  });

  final int id;
  final String name;
  final int trainingPoints;
  final String? campus;
  final DateTime? startTime;
  final DateTime? registrationCloseTime;
  final bool? isRegistrationOpen;
  final String? myRegistrationStatus;

  factory AssistantActivity.fromJson(Map<String, dynamic> json) => AssistantActivity(
        id: json['id'] as int,
        name: json['activityName'] as String? ?? '',
        trainingPoints: json['trainingPoints'] as int? ?? 0,
        campus: json['campus'] as String?,
        startTime: VnTime.parseOrNull(json['startTime']),
        registrationCloseTime: VnTime.parseOrNull(json['registrationCloseTime']),
        isRegistrationOpen: json['isRegistrationOpen'] as bool?,
        myRegistrationStatus: json['myRegistrationStatus'] as String?,
      );
}

/// `POST /api/v1/assistant/chat` answer.
class AssistantReply {
  const AssistantReply({
    required this.reply,
    required this.activities,
    required this.toolsUsed,
    required this.droppedActivityReferences,
    this.replySignature = '',
    this.personalDataRedacted = false,
    this.remainingToday,
  });

  final String reply;
  final List<AssistantActivity> activities;
  final List<String> toolsUsed;
  final int droppedActivityReferences;

  /// API signature of this answer (US-32); sent back with it, or the API rejects the conversation.
  final String replySignature;

  /// True when the API hid personal data (name, student code, email, phone) before calling Gemini.
  final bool personalDataRedacted;
  final int? remainingToday;

  factory AssistantReply.fromJson(Map<String, dynamic> json) => AssistantReply(
        reply: json['reply'] as String? ?? '',
        activities: ((json['activities'] as List?) ?? const [])
            .map((raw) => AssistantActivity.fromJson((raw as Map).cast<String, dynamic>()))
            .toList(),
        toolsUsed: ((json['toolsUsed'] as List?) ?? const []).map((item) => item.toString()).toList(),
        droppedActivityReferences: json['droppedActivityReferences'] as int? ?? 0,
        replySignature: json['replySignature'] as String? ?? '',
        personalDataRedacted: json['personalDataRedacted'] as bool? ?? false,
        remainingToday: json['remainingToday'] as int?,
      );
}

enum ChatRole { user, assistant }

/// One bubble of the conversation. The app keeps the conversation (the API stores nothing) and sends the
/// successful turns back with each question.
class ChatMessage {
  const ChatMessage({
    required this.role,
    required this.text,
    this.activities = const [],
    this.toolsUsed = const [],
    this.isError = false,
    this.inHistory = true,
    this.signature,
    this.personalDataRedacted = false,
  });

  ChatMessage.user(String text) : this(role: ChatRole.user, text: text);

  ChatMessage.assistant(AssistantReply reply)
      : this(
          role: ChatRole.assistant,
          text: reply.reply,
          activities: reply.activities,
          toolsUsed: reply.toolsUsed,
          signature: reply.replySignature,
          personalDataRedacted: reply.personalDataRedacted,
        );

  /// An error shown to the user; never sent back to the assistant.
  ChatMessage.error(String text) : this(role: ChatRole.assistant, text: text, isError: true, inHistory: false);

  final ChatRole role;
  final String text;
  final List<AssistantActivity> activities;
  final List<String> toolsUsed;
  final bool isError;

  /// False for errors and for a question that got no answer, so the next request stays user/assistant alternating.
  final bool inHistory;

  /// API signature of an assistant answer; the API only accepts assistant turns that carry it.
  final String? signature;
  final bool personalDataRedacted;

  ChatMessage withoutHistory() => ChatMessage(
        role: role,
        text: text,
        activities: activities,
        toolsUsed: toolsUsed,
        isError: isError,
        inHistory: false,
        signature: signature,
        personalDataRedacted: personalDataRedacted,
      );

  Map<String, String> toJson() => {
        'role': role == ChatRole.user ? 'user' : 'assistant',
        'content': text,
        if (role == ChatRole.assistant && signature != null) 'signature': signature!,
      };

  static const _toolLabels = {
    'get_points_summary': 'điểm và mục tiêu của kỳ',
    'list_evaluation_periods': 'các kỳ đánh giá',
    'get_my_activity_history': 'lịch sử hoạt động của bạn',
    'search_activities': 'hoạt động phù hợp',
    'get_activity_detail': 'chi tiết hoạt động',
  };

  /// What the answer looked up, in Vietnamese ("Đã tra cứu: ...").
  List<String> get toolLabels => toolsUsed.map((tool) => _toolLabels[tool] ?? tool).toList();
}
