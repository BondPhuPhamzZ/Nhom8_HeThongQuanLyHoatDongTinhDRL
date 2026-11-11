import '../models/participation.dart';
import 'api_client.dart';

/// US-07 check-in by session code (typed or scanned from the QR); attendance is the proof of participation.
class ParticipationService {
  final _api = ApiClient.instance;

  Future<CheckInStatus> checkInStatus(int activityId) async =>
      CheckInStatus.fromJson((await _api.get('/api/v1/student-activities/$activityId/check-in-status')).map);

  Future<String> checkIn(int activityId, int sessionId, String code) async =>
      (await _api.post('/api/v1/student-activities/$activityId/sessions/$sessionId/check-in', {'code': code.trim().toUpperCase()})).message;
}
