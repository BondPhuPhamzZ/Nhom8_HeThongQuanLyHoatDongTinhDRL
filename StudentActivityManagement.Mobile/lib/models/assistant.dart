import '../utils/vn_time.dart';

/// `GET /api/v1/assistant/status`: the chat is shown only when the server has the assistant enabled.
class AssistantStatus {
  const AssistantStatus({
    required this.enabled,
    this.model,
    this.dailyQuestionLimit = 0,
    this.remainingToday = 0,
    this.dailyTokenLimit = 0,
    this.tokensUsedToday = 0,
  });

  final bool enabled;
  final String? model;
  final int dailyQuestionLimit;
  final int remainingToday;
  final int dailyTokenLimit;
  final int tokensUsedToday;

  factory AssistantStatus.fromJson(Map<String, dynamic> json) => AssistantStatus(
        enabled: json['enabled'] as bool? ?? false,
        model: json['model'] as String?,
        dailyQuestionLimit: json['dailyQuestionLimit'] as int? ?? 0,
        remainingToday: json['remainingToday'] as int? ?? 0,
        dailyTokenLimit: json['dailyTokenLimit'] as int? ?? 0,
        tokensUsedToday: json['tokensUsedToday'] as int? ?? 0,
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
    this.conversationId = 0,
    this.messageId = 0,
    this.personalDataRedacted = false,
    this.remainingToday,
    this.tokensUsedToday,
  });

  final String reply;
  final List<AssistantActivity> activities;
  final List<String> toolsUsed;
  final int droppedActivityReferences;

  final int conversationId;
  final int messageId;

  /// True when the API hid personal data (name, student code, email, phone) before calling Gemini.
  final bool personalDataRedacted;
  final int? remainingToday;
  final int? tokensUsedToday;

  factory AssistantReply.fromJson(Map<String, dynamic> json) => AssistantReply(
        reply: json['reply'] as String? ?? '',
        activities: ((json['activities'] as List?) ?? const [])
            .map((raw) => AssistantActivity.fromJson((raw as Map).cast<String, dynamic>()))
            .toList(),
        toolsUsed: ((json['toolsUsed'] as List?) ?? const []).map((item) => item.toString()).toList(),
        droppedActivityReferences: json['droppedActivityReferences'] as int? ?? 0,
        conversationId: json['conversationId'] as int? ?? 0,
        messageId: json['messageId'] as int? ?? 0,
        personalDataRedacted: json['personalDataRedacted'] as bool? ?? false,
        remainingToday: json['remainingToday'] as int?,
        tokensUsedToday: json['tokensUsedToday'] as int?,
      );
}

enum ChatRole { user, assistant }

class AssistantConversation {
  const AssistantConversation({
    required this.id,
    required this.title,
    required this.createdAt,
    required this.lastMessageAt,
    required this.messageCount,
    required this.messages,
  });

  final int id;
  final String title;
  final DateTime createdAt;
  final DateTime lastMessageAt;
  final int messageCount;
  final List<ChatMessage> messages;

  factory AssistantConversation.fromJson(Map<String, dynamic> json) => AssistantConversation(
        id: json['id'] as int,
        title: json['title'] as String? ?? '',
        createdAt: VnTime.parse(json['createdAt']),
        lastMessageAt: VnTime.parse(json['lastMessageAt']),
        messageCount: json['messageCount'] as int? ?? 0,
        messages: ((json['messages'] as List?) ?? const [])
            .map((raw) => ChatMessage.fromJson((raw as Map).cast<String, dynamic>()))
            .toList(),
      );
}

/// One bubble in a conversation stored by the API.
class ChatMessage {
  const ChatMessage({
    required this.role,
    required this.text,
    this.activities = const [],
    this.toolsUsed = const [],
    this.isError = false,
    this.createdAt,
    this.personalDataRedacted = false,
  });

  ChatMessage.user(String text) : this(role: ChatRole.user, text: text);

  ChatMessage.assistant(AssistantReply reply)
      : this(
          role: ChatRole.assistant,
          text: reply.reply,
          activities: reply.activities,
          toolsUsed: reply.toolsUsed,
          personalDataRedacted: reply.personalDataRedacted,
        );

  ChatMessage.error(String text) : this(role: ChatRole.assistant, text: text, isError: true);

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
        role: json['role'] == 'user' ? ChatRole.user : ChatRole.assistant,
        text: json['content'] as String? ?? '',
        createdAt: VnTime.parseOrNull(json['createdAt']),
        isError: json['status'] == 'Failed',
        personalDataRedacted: json['personalDataRedacted'] as bool? ?? false,
        toolsUsed: ((json['toolsUsed'] as List?) ?? const []).cast<String>(),
        activities: ((json['activities'] as List?) ?? const [])
            .map((raw) => AssistantActivity.fromJson((raw as Map).cast<String, dynamic>()))
            .toList(),
      );

  final ChatRole role;
  final String text;
  final List<AssistantActivity> activities;
  final List<String> toolsUsed;
  final bool isError;

  final DateTime? createdAt;
  final bool personalDataRedacted;

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
