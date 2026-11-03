import '../models/app_notification.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// The signed-in student's notifications; the API decides who receives what (US-06, US-12).
class NotificationService {
  final _api = ApiClient.instance;

  Future<PagedResult<AppNotification>> list({bool unreadOnly = false, int page = 1, int pageSize = 20}) async {
    final result = await _api.get('/api/v1/notifications', {
      if (unreadOnly) 'unreadOnly': 'true',
      'pageIndex': page,
      'pageSize': pageSize,
    });
    return PagedResult.fromJson(result.map, AppNotification.fromJson);
  }

  Future<void> markRead(int id) async => _api.post('/api/v1/notifications/$id/read');
}
