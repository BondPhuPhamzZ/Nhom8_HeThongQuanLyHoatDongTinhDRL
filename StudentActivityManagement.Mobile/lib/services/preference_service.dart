import 'package:shared_preferences/shared_preferences.dart';

import '../constants/api_config.dart';

/// Keeps the session (JWT and its expiry) and the API address between launches.
class PreferenceService {
  PreferenceService._();
  static final instance = PreferenceService._();

  static const _tokenKey = 'auth.token';
  static const _expiresKey = 'auth.expiresAt';
  static const _baseUrlKey = 'api.baseUrl';

  SharedPreferences? _prefs;

  Future<void> init() async => _prefs ??= await SharedPreferences.getInstance();

  SharedPreferences get _store {
    final prefs = _prefs;
    if (prefs == null) throw StateError('PreferenceService.init() must run before use.');
    return prefs;
  }

  String get baseUrl => _store.getString(_baseUrlKey) ?? ApiConfig.defaultBaseUrl;

  Future<void> setBaseUrl(String value) async {
    final trimmed = value.trim().replaceAll(RegExp(r'/+$'), '');
    if (trimmed.isEmpty) {
      await _store.remove(_baseUrlKey);
    } else {
      await _store.setString(_baseUrlKey, trimmed);
    }
  }

  String? get token => _store.getString(_tokenKey);

  DateTime? get tokenExpiresAt {
    final raw = _store.getString(_expiresKey);
    return raw == null ? null : DateTime.tryParse(raw);
  }

  Future<void> saveSession(String token, DateTime expiresAt) async {
    await _store.setString(_tokenKey, token);
    await _store.setString(_expiresKey, expiresAt.toUtc().toIso8601String());
  }

  Future<void> clearSession() async {
    await _store.remove(_tokenKey);
    await _store.remove(_expiresKey);
  }
}
