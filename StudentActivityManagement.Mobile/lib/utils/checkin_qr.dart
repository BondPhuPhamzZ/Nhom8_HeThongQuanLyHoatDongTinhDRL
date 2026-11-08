/// US-35: content of the check-in QR the Web shows for a session, `nhom8-checkin:v1:{activityId}:{sessionId}:{code}`.
/// Scanning only fills in the code; the API still checks the code, the check-in window and the registration.
class CheckInQr {
  const CheckInQr({required this.activityId, required this.sessionId, required this.code});

  static const prefix = 'nhom8-checkin:v1';
  static final _code = RegExp(r'^[A-Za-z0-9]{4,20}$');

  final int activityId;
  final int sessionId;
  final String code;

  /// Null for anything that is not a check-in QR of this system (a URL, another app's QR, a damaged value).
  static CheckInQr? parse(String? raw) {
    final parts = (raw ?? '').trim().split(':');
    if (parts.length != 5 || '${parts[0]}:${parts[1]}' != prefix) return null;
    final activityId = int.tryParse(parts[2]);
    final sessionId = int.tryParse(parts[3]);
    final code = parts[4];
    if (activityId == null || activityId <= 0 || sessionId == null || sessionId <= 0 || !_code.hasMatch(code)) return null;
    return CheckInQr(activityId: activityId, sessionId: sessionId, code: code.toUpperCase());
  }

  @override
  String toString() => '$prefix:$activityId:$sessionId:$code';
}
