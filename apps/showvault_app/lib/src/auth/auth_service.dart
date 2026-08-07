import 'dart:io';

import 'package:auth0_flutter/auth0_flutter.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/config/app_config.dart';

class AuthService {
  AuthService()
    : _auth0 = Auth0(AppConfig.auth0Domain, AppConfig.auth0ClientId);

  final Auth0 _auth0;
  AuthSession? _windowsSession;

  Future<AuthSession?> restore() async {
    if (!AppConfig.hasAuth0Client) return null;
    if (Platform.isWindows) return _windowsSession;
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
      _windowsSession = _toSession(credentials);
      return _windowsSession!;
    }

    credentials = await _auth0.webAuthentication().login(
      audience: AppConfig.auth0Audience,
      useHTTPS: true,
    );
    return _toSession(credentials);
  }

  Future<void> logout() async {
    if (Platform.isWindows) {
      await _auth0.windowsWebAuthentication().logout(
        appCustomURL: AppConfig.windowsCallbackUrl,
      );
      _windowsSession = null;
      return;
    }
    await _auth0.webAuthentication().logout(useHTTPS: true);
  }

  AuthSession _toSession(Credentials credentials) => AuthSession(
    accessToken: credentials.accessToken,
    displayName:
        credentials.user.name ?? credentials.user.email ?? 'ShowVault operator',
  );
}
