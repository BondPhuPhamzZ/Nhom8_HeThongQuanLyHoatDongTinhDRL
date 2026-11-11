import '../models/activity.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// US-02..US-05: activities the student may join, their details, registration and cancellation.
class ActivityService {
  final _api = ApiClient.instance;

  /// [registered] lists the student's own registrations, ended activities included (to see attendance and points).
  Future<PagedResult<Activity>> list({String? search, String? campus, int page = 1, int pageSize = 10, bool registered = false}) async {
    final result = await _api.get('/api/v1/student-activities', {
      'search': search,
      'campus': campus,
      'pageIndex': page,
      'pageSize': pageSize,
      if (registered) 'registered': 'true',
    });
    return PagedResult.fromJson(result.map, Activity.fromJson);
  }

  Future<Activity> detail(int id) async => Activity.fromJson((await _api.get('/api/v1/student-activities/$id')).map);

  Future<String> register(int id) async => (await _api.post('/api/v1/student-activities/$id/register')).message;

  Future<String> cancel(int id) async => (await _api.delete('/api/v1/student-activities/$id/registration')).message;
}
