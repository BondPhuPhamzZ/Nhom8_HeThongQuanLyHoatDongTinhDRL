import '../models/class_space.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// US-09 class discussion, US-13/14 monitor announcements and answers, US-15/16 escalations and relays.
/// The class always comes from the signed-in account; the API returns 404 for other classes.
class ClassSpaceService {
  final _api = ApiClient.instance;
  static const _base = '/api/v1/class-space';

  Future<ClassSpace> threads({String? kind, String? search, int page = 1, int pageSize = 15}) async =>
      ClassSpace.fromJson((await _api.get('$_base/threads', {'kind': kind, 'search': search, 'pageIndex': page, 'pageSize': pageSize})).map);

  Future<ThreadDetail> thread(int id) async => ThreadDetail.fromJson((await _api.get('$_base/threads/$id')).map);

  /// Returns the new thread's id and the API message.
  Future<(int, String)> create({required String kind, required String title, required String body}) async {
    final result = await _api.post('$_base/threads', {'kind': kind, 'title': title.trim(), 'body': body.trim()});
    return (result.map['id'] as int, result.message);
  }

  Future<String> reply(int threadId, String body) async => (await _api.post('$_base/threads/$threadId/replies', {'body': body.trim()})).message;

  Future<String> setPinned(int threadId, bool isPinned) async => (await _api.patch('$_base/threads/$threadId/pin', {'isPinned': isPinned})).message;

  Future<String> markAnswer(int threadId, int replyId) async => (await _api.post('$_base/threads/$threadId/replies/$replyId/answer')).message;

  Future<String> deleteThread(int threadId) async => (await _api.delete('$_base/threads/$threadId')).message;

  Future<String> deleteReply(int threadId, int replyId) async => (await _api.delete('$_base/threads/$threadId/replies/$replyId')).message;

  Future<String> escalate(int threadId, String question) async =>
      (await _api.post('$_base/threads/$threadId/escalations', {'question': question.trim()})).message;

  Future<PagedResult<Escalation>> escalations({String? status, int page = 1}) async =>
      PagedResult.fromJson((await _api.get('$_base/escalations', {'status': status, 'pageIndex': page, 'pageSize': 20})).map, Escalation.fromJson);

  Future<String> withdraw(int escalationId) async => (await _api.post('$_base/escalations/$escalationId/withdraw')).message;

  Future<String> relay(int escalationId) async => (await _api.post('$_base/escalations/$escalationId/relay')).message;
}
