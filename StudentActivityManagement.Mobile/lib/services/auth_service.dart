import '../models/user.dart';
import '../utils/vn_time.dart';
import 'api_client.dart';
import 'preference_service.dart';

/// US-01: sign in with the shared JWT endpoint. US-44: signing out revokes the account's tokens on every device.
class AuthService {
  final _api = ApiClient.instance;
  final _prefs = PreferenceService.instance;

  Future<AppUser> login(String usernameOrEmail, String password) async {
    final result = await _api.post('/api/v1/auth/login', {'usernameOrEmail': usernameOrEmail.trim(), 'password': password});
    return _saveSession(result.map);
  }

  Future<AppUser> updateContact(String email, String phone, String currentPassword) async {
    await _api.put('/api/v1/me/profile', {'email': email, 'phone': phone, 'currentPassword': currentPassword});
    return AppUser.fromJson((await _api.get('/api/v1/auth/me')).map);
  }

  Future<AppUser> changePassword(String currentPassword, String newPassword) async {
    final result = await _api.post('/api/v1/me/change-password', {
      'currentPassword': currentPassword,
      'newPassword': newPassword,
    });
    return _saveSession(result.map);
  }

  Future<AppUser> _saveSession(Map<String, dynamic> data) async {
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

  /// Asks the API to revoke every token of the account (US-44), then forgets the local one. The local session
  /// ends even when the API is unreachable or the token was already revoked elsewhere.
  Future<void> logout() async {
    try {
      await _api.postLong('/api/v1/auth/logout', null, const Duration(seconds: 5));
    } on ApiException {
      // Offline, or already signed out from another device.
    }
    await _prefs.clearSession();
  }

  /// Forgets the local session only: used when the API has already rejected the token.
  Future<void> forget() => _prefs.clearSession();
}
