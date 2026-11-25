import '../models/assistant.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// US-31 training-point assistant (Gemini on the API side, answers from the student's own data).
class AssistantService {
  final _api = ApiClient.instance;

  static const chatTimeout = Duration(seconds: 90);

  Future<AssistantStatus> status() async => AssistantStatus.fromJson((await _api.get('/api/v1/assistant/status')).map);

  Future<PagedResult<AssistantConversation>> listConversations({int page = 1}) async => PagedResult.fromJson(
        (await _api.get('/api/v1/assistant/conversations?page=$page')).map,
        AssistantConversation.fromJson,
      );

  Future<AssistantConversation> getConversation(int id) async =>
      AssistantConversation.fromJson((await _api.get('/api/v1/assistant/conversations/$id')).map);

  Future<AssistantReply> chat(int? conversationId, String message) async {
    final result = await _api.postLong(
      '/api/v1/assistant/chat',
      {'conversationId': conversationId, 'message': message},
      chatTimeout,
    );
    return AssistantReply.fromJson(result.map);
  }
}
