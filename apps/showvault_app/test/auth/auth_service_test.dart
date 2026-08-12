import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/auth/auth_service.dart';
import 'package:showvault_app/src/auth/auth_session.dart';

const _session = AuthSession(
  accessToken: 'test-access-token',
  displayName: 'Test operator',
);

class _FakeAuthClient implements AuthClient {
  int restoreCredentialsCalls = 0;
  int loginWebCalls = 0;
  int loginWindowsCalls = 0;
  int logoutWebCalls = 0;
  int logoutWindowsCalls = 0;

  @override
  Future<AuthSession?> restoreCredentials() async {
    restoreCredentialsCalls += 1;
    return _session;
  }

  @override
  Future<AuthSession> loginWeb() async {
    loginWebCalls += 1;
    return _session;
  }

  @override
  Future<AuthSession> loginWindows() async {
    loginWindowsCalls += 1;
    return _session;
  }

  @override
  Future<void> logoutWeb() async {
    logoutWebCalls += 1;
  }

  @override
  Future<void> logoutWindows() async {
    logoutWindowsCalls += 1;
  }
}

void main() {
  group('macOS session handling', () {
    const platform = AuthRuntimePlatform(isWindows: false, isMacOS: true);

    test('never restores credentials from persistent storage', () async {
      final client = _FakeAuthClient();
      final service = AuthService(
        client: client,
        platform: platform,
        hasAuth0Client: true,
      );

      expect(await service.restore(), isNull);
      expect(client.restoreCredentialsCalls, 0);
    });

    test('keeps the web login session in memory until logout', () async {
      final client = _FakeAuthClient();
      final service = AuthService(
        client: client,
        platform: platform,
        hasAuth0Client: true,
      );

      expect(await service.login(), _session);
      expect(await service.restore(), _session);
      expect(client.loginWebCalls, 1);
      expect(client.restoreCredentialsCalls, 0);

      await service.logout();

      expect(client.logoutWebCalls, 1);
      expect(await service.restore(), isNull);
      expect(client.restoreCredentialsCalls, 0);
    });
  });

  test('mobile restores through the credentials manager', () async {
    final client = _FakeAuthClient();
    final service = AuthService(
      client: client,
      platform: const AuthRuntimePlatform(isWindows: false, isMacOS: false),
      hasAuth0Client: true,
    );

    expect(await service.restore(), _session);
    expect(client.restoreCredentialsCalls, 1);
  });

  test('Windows uses only its in-memory and native web-auth paths', () async {
    final client = _FakeAuthClient();
    final service = AuthService(
      client: client,
      platform: const AuthRuntimePlatform(isWindows: true, isMacOS: false),
      hasAuth0Client: true,
    );

    expect(await service.restore(), isNull);
    expect(await service.login(), _session);
    expect(await service.restore(), _session);
    expect(client.loginWindowsCalls, 1);
    expect(client.restoreCredentialsCalls, 0);

    await service.logout();

    expect(client.logoutWindowsCalls, 1);
    expect(await service.restore(), isNull);
  });

  test('missing Auth0 configuration skips all restoration', () async {
    final client = _FakeAuthClient();
    final service = AuthService(
      client: client,
      platform: const AuthRuntimePlatform(isWindows: false, isMacOS: false),
      hasAuth0Client: false,
    );

    expect(await service.restore(), isNull);
    expect(client.restoreCredentialsCalls, 0);
  });
}
