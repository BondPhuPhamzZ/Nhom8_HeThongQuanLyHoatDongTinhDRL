import 'package:flutter/foundation.dart';

import '../models/user.dart';
import '../services/api_client.dart';
import '../services/auth_service.dart';

enum AuthStatus { unknown, signedOut, signedIn }

/// Session state for the whole app. A 401 from any API call signs the user out.
class AuthProvider extends ChangeNotifier {
  AuthProvider({AuthService? service}) : _service = service ?? AuthService() {
    ApiClient.instance.onUnauthorized = () => _expire(
        'Phiên đăng nhập đã kết thúc (hết hạn, đã đăng xuất ở thiết bị khác hoặc tài khoản đã thay đổi quyền). Vui lòng đăng nhập lại.');
  }

  final AuthService _service;

  AuthStatus status = AuthStatus.unknown;
  AppUser? user;

  /// Shown once on the login screen after a forced sign-out.
  String? notice;

  Future<void> restore() async {
    try {
      user = await _service.restore();
      status = user == null ? AuthStatus.signedOut : AuthStatus.signedIn;
    } on ApiException catch (error) {
      // API unreachable at launch: go to the login screen and say why.
      user = null;
      status = AuthStatus.signedOut;
      notice = error.message;
    }
    notifyListeners();
  }

  Future<void> login(String usernameOrEmail, String password) async {
    user = await _service.login(usernameOrEmail, password);
    status = AuthStatus.signedIn;
    notice = null;
    notifyListeners();
  }

  /// US-44: signs out here and on every other device of the account.
  Future<void> logout() async {
    // Leave the signed-in state first, so a 401 answered by the logout call itself cannot loop back here.
    user = null;
    status = AuthStatus.signedOut;
    notifyListeners();
    await _service.logout();
  }

  /// The API already rejected the token (expired, revoked from another device, account changed).
  void _expire(String message) {
    if (status != AuthStatus.signedIn) return;
    notice = message;
    user = null;
    status = AuthStatus.signedOut;
    notifyListeners();
    _service.forget();
  }
}
