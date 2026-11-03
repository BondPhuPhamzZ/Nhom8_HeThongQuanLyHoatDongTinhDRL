import 'package:flutter/foundation.dart';

/// Where the shared ASP.NET Core API runs. Order: the address saved in the app's settings, then
/// `--dart-define=API_BASE_URL=...`, then a default for the platform (the Android emulator reaches the
/// host machine at 10.0.2.2).
abstract final class ApiConfig {
  static const fromEnvironment = String.fromEnvironment('API_BASE_URL');

  static String get defaultBaseUrl {
    if (fromEnvironment.isNotEmpty) return fromEnvironment;
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) return 'http://10.0.2.2:5210';
    return 'http://localhost:5210';
  }

  static const requestTimeout = Duration(seconds: 20);
}
