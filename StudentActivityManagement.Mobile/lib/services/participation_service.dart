import '../models/participation.dart';
import 'api_client.dart';

/// US-07 check-in by session code and US-08 evidence of participation.
class ParticipationService {
  final _api = ApiClient.instance;

  Future<CheckInStatus> checkInStatus(int activityId) async =>
      CheckInStatus.fromJson((await _api.get('/api/v1/student-activities/$activityId/check-in-status')).map);

  Future<String> checkIn(int activityId, int sessionId, String code) async =>
      (await _api.post('/api/v1/student-activities/$activityId/sessions/$sessionId/check-in', {'code': code.trim().toUpperCase()})).message;

  Future<List<Evidence>> evidences(int activityId) async => (await _api.get('/api/v1/student-activities/$activityId/evidences'))
      .list
      .map((raw) => Evidence.fromJson((raw as Map).cast<String, dynamic>()))
      .toList();

  /// [evidenceType] is Image, Document (with a file) or ConfirmationCode (with [confirmationCode]).
  Future<String> submitEvidence(
    int activityId, {
    required String evidenceType,
    int? sessionId,
    String? confirmationCode,
    String? note,
    List<int>? fileBytes,
    String? fileName,
  }) async {
    final result = await _api.postMultipart(
      '/api/v1/student-activities/$activityId/evidences',
      {
        'evidenceType': evidenceType,
        if (sessionId != null) 'sessionId': '$sessionId',
        if (confirmationCode != null && confirmationCode.trim().isNotEmpty) 'confirmationCode': confirmationCode.trim(),
        if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      },
      fileField: fileBytes == null ? null : 'file',
      fileBytes: fileBytes,
      fileName: fileName,
    );
    return result.message;
  }

  Future<String> deleteEvidence(int activityId, int evidenceId) async =>
      (await _api.delete('/api/v1/student-activities/$activityId/evidences/$evidenceId')).message;
}
