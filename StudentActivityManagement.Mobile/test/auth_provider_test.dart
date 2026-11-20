import 'package:flutter_test/flutter_test.dart';
import 'package:nhom8_qlhd_tinh_drl/providers/auth_provider.dart';
import 'package:nhom8_qlhd_tinh_drl/services/api_client.dart';
import 'package:nhom8_qlhd_tinh_drl/services/auth_service.dart';

/// Records what the provider asked for, without touching the network or storage.
class _FakeAuthService extends AuthService {
  int logouts = 0;
  int forgets = 0;

  @override
  Future<void> logout() async => logouts++;

  @override
  Future<void> forget() async => forgets++;
}

/// US-44: signing out revokes every device; a session revoked elsewhere is only forgotten here.
void main() {
  test('logout leaves the signed-in state and asks the API to revoke every device', () async {
    final service = _FakeAuthService();
    final provider = AuthProvider(service: service)..status = AuthStatus.signedIn;

    await provider.logout();

    expect(provider.status, AuthStatus.signedOut);
    expect(provider.user, isNull);
    expect(service.logouts, 1);
    expect(service.forgets, 0);
  });

  test('a 401 after logging out on another device forgets the session once and says why', () {
    final service = _FakeAuthService();
    final provider = AuthProvider(service: service)..status = AuthStatus.signedIn;

    ApiClient.instance.onUnauthorized!();
    ApiClient.instance.onUnauthorized!();

    expect(provider.status, AuthStatus.signedOut);
    expect(provider.notice, contains('đăng xuất ở thiết bị khác'));
    expect(service.forgets, 1);
    expect(service.logouts, 0);
  });
}
