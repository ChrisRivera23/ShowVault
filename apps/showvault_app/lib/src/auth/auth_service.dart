import 'dart:io';

import 'package:auth0_flutter/auth0_flutter.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/config/app_config.dart';

class AuthRuntimePlatform {
  const AuthRuntimePlatform({required this.isWindows, required this.isMacOS});

  factory AuthRuntimePlatform.current() => AuthRuntimePlatform(
    isWindows: Platform.isWindows,
    isMacOS: Platform.isMacOS,
  );

  final bool isWindows;
  final bool isMacOS;
}

abstract interface class AuthClient {
  Future<AuthSession?> restoreCredentials();

  Future<AuthSession> loginWeb();

  Future<AuthSession> loginWindows();

  Future<void> logoutWeb();

  Future<void> logoutWindows();
}

class Auth0Client implements AuthClient {
  Auth0Client()
    : _auth0 = Auth0(AppConfig.auth0Domain, AppConfig.auth0ClientId);

  final Auth0 _auth0;

  @override
  Future<AuthSession?> restoreCredentials() async {
    if (!await _auth0.credentialsManager.hasValidCredentials()) return null;
    return _toSession(await _auth0.credentialsManager.credentials());
  }

  @override
  Future<AuthSession> loginWeb() async => _toSession(
    await _auth0.webAuthentication().login(audience: AppConfig.auth0Audience),
  );

  @override
  Future<AuthSession> loginWindows() async => _toSession(
    await _auth0.windowsWebAuthentication().login(
      appCustomURL: AppConfig.windowsCallbackUrl,
      audience: AppConfig.auth0Audience,
    ),
  );

  @override
  Future<void> logoutWeb() => _auth0.webAuthentication().logout();

  @override
  Future<void> logoutWindows() => _auth0.windowsWebAuthentication().logout(
    appCustomURL: AppConfig.windowsCallbackUrl,
  );

  AuthSession _toSession(Credentials credentials) => AuthSession(
    accessToken: credentials.accessToken,
    displayName:
        credentials.user.name ?? credentials.user.email ?? 'ShowVault operator',
  );
}

class AuthService {
  AuthService({
    AuthClient? client,
    AuthRuntimePlatform? platform,
    bool? hasAuth0Client,
  }) : _client = client ?? Auth0Client(),
       _platform = platform ?? AuthRuntimePlatform.current(),
       _hasAuth0Client = hasAuth0Client ?? AppConfig.hasAuth0Client;

  final AuthClient _client;
  final AuthRuntimePlatform _platform;
  final bool _hasAuth0Client;
  AuthSession? _memorySession;

  Future<AuthSession?> restore() async {
    if (!_hasAuth0Client) return null;
    if (_platform.isWindows || _platform.isMacOS) return _memorySession;
    return _client.restoreCredentials();
  }

  Future<AuthSession> login() async {
    if (_platform.isWindows) {
      _memorySession = await _client.loginWindows();
      return _memorySession!;
    }

    final session = await _client.loginWeb();
    if (_platform.isMacOS) _memorySession = session;
    return session;
  }

  Future<void> logout() async {
    if (_platform.isWindows) {
      await _client.logoutWindows();
      _memorySession = null;
      return;
    }
    await _client.logoutWeb();
    if (_platform.isMacOS) _memorySession = null;
  }
}
