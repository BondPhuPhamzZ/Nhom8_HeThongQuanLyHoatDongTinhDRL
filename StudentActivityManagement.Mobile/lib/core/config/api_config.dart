abstract final class ApiConfig {
  // Android emulator: http://10.0.2.2:5210 ; physical device: host LAN IP.
  // Supply with --dart-define=API_BASE_URL=... when running the app.
  static const baseUrl = String.fromEnvironment('API_BASE_URL');
  static bool get isConfigured => baseUrl.isNotEmpty;
}
