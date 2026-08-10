import 'dart:io';

import 'package:auth0_flutter/auth0_flutter.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/config/app_config.dart';

class AuthService {
  AuthService()
    : _auth0 = Auth0(AppConfig.auth0Domain, AppConfig.auth0ClientId);

  final Auth0 _auth0;
  AuthSession? _memorySession;

  Future<AuthSession?> restore() async {
    if (!AppConfig.hasAuth0Client) return null;
    if (Platform.isWindows || Platform.isMacOS) return _memorySession;
    if (!await _auth0.credentialsManager.hasValidCredentials()) return null;
    return _toSession(await _auth0.credentialsManager.credentials());
  }

  Future<AuthSession> login() async {
    final Credentials credentials;
    if (Platform.isWindows) {
      credentials = await _auth0.windowsWebAuthentication().login(
        appCustomURL: AppConfig.windowsCallbackUrl,
        audience: AppConfig.auth0Audience,
      );
      _memorySession = _toSession(credentials);
      return _memorySession!;
    }

    credentials = await _auth0.webAuthentication().login(
      audience: AppConfig.auth0Audience,
    );
    if (Platform.isMacOS) {
      _memorySession = _toSession(credentials);
      return _memorySession!;
    }
    return _toSession(credentials);
  }

  Future<void> logout() async {
    if (Platform.isWindows) {
      await _auth0.windowsWebAuthentication().logout(
        appCustomURL: AppConfig.windowsCallbackUrl,
      );
      _memorySession = null;
      return;
    }
    await _auth0.webAuthentication().logout();
    if (Platform.isMacOS) _memorySession = null;
  }

  AuthSession _toSession(Credentials credentials) => AuthSession(
    accessToken: credentials.accessToken,
    displayName:
        credentials.user.name ?? credentials.user.email ?? 'ShowVault operator',
  );
}
