import '../models/student_report.dart';
import 'api_client.dart';

/// US-10 history and US-11 points/recommendations, per evaluation period.
class ReportService {
  final _api = ApiClient.instance;

  Future<List<EvaluationPeriod>> periods() async => (await _api.get('/api/v1/student/reports/evaluation-periods'))
      .list
      .map((raw) => EvaluationPeriod.fromJson((raw as Map).cast<String, dynamic>()))
      .toList();

  Future<List<ActivityHistoryItem>> history(int periodId, {String? registrationStatus}) async {
    final result = await _api.get('/api/v1/student/reports/activity-history', {
      'periodId': periodId,
      'registrationStatus': registrationStatus,
    });
    return ((result.map['activities'] as List?) ?? const [])
        .map((raw) => ActivityHistoryItem.fromJson((raw as Map).cast<String, dynamic>()))
        .toList();
  }

  /// Null when no evaluation period exists yet (the API answers 404).
  Future<PointsSummary?> points(int? periodId) async {
    try {
      return PointsSummary.fromJson((await _api.get('/api/v1/student/reports/points-and-recommendations', {'periodId': periodId})).map);
    } on ApiException catch (error) {
      if (error.statusCode == 404) return null;
      rethrow;
    }
  }
}
