import '../models/user.dart';
import '../utils/vn_time.dart';
import 'api_client.dart';
import 'preference_service.dart';

/// US-01: sign in with the shared JWT endpoint. Signing out only forgets the token, as on the Web.
class AuthService {
  final _api = ApiClient.instance;
  final _prefs = PreferenceService.instance;

  Future<AppUser> login(String usernameOrEmail, String password) async {
    final result = await _api.post('/api/v1/auth/login', {'usernameOrEmail': usernameOrEmail.trim(), 'password': password});
    final data = result.map;
    final user = AppUser.fromJson((data['user'] as Map).cast<String, dynamic>());
    if (!user.isStudentRole) {
      throw ApiException('Ứng dụng di động dành cho sinh viên và lớp trưởng. Cán bộ Phòng QL HSSV và quản trị viên dùng bản Web.');
    }
    await _prefs.saveSession(data['token'] as String, VnTime.parse(data['expiresAt']));
    return user;
  }

  /// Validates the stored token with `/auth/me`; the API rejects tokens of locked accounts or changed roles.
  Future<AppUser?> restore() async {
    final token = _prefs.token;
    final expiresAt = _prefs.tokenExpiresAt;
    if (token == null || (expiresAt != null && expiresAt.isBefore(DateTime.now().toUtc()))) {
      await _prefs.clearSession();
      return null;
    }
    try {
      final user = AppUser.fromJson((await _api.get('/api/v1/auth/me')).map);
      if (user.isStudentRole) return user;
    } on ApiException catch (error) {
      if (!error.isUnauthorized) rethrow;
    }
    await _prefs.clearSession();
    return null;
  }

  Future<void> logout() => _prefs.clearSession();
}
