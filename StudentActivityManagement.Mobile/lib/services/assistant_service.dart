import '../models/assistant.dart';
import 'api_client.dart';

/// US-31 training-point assistant (Gemini on the API side, answers from the student's own data).
class AssistantService {
  final _api = ApiClient.instance;

  /// The API accepts 12 messages; an odd window keeps the conversation starting with a question.
  static const maxMessages = 11;
  static const chatTimeout = Duration(seconds: 90);

  Future<AssistantStatus> status() async => AssistantStatus.fromJson((await _api.get('/api/v1/assistant/status')).map);

  /// Sends the successful turns of [conversation]; its last message must be the new question.
  Future<AssistantReply> chat(List<ChatMessage> conversation) async {
    final history = conversation.where((message) => message.inHistory).toList();
    final window = history.length > maxMessages ? history.sublist(history.length - maxMessages) : history;
    final result = await _api.postLong(
      '/api/v1/assistant/chat',
      {'messages': window.map((message) => message.toJson()).toList()},
      chatTimeout,
    );
    return AssistantReply.fromJson(result.map);
  }
}
