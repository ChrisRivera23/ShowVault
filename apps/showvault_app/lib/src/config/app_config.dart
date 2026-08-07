class AppConfig {
  const AppConfig._();

  static const auth0Domain = String.fromEnvironment(
    'AUTH0_DOMAIN',
    defaultValue: 'dev-4m7moxkl7dikmtf7.us.auth0.com',
  );
  static const auth0ClientId = String.fromEnvironment(
    'AUTH0_CLIENT_ID',
    defaultValue: 'wxisYnuTvRe3fMg23m5TPoIQh8vOWWKl',
  );
  static const auth0Audience = String.fromEnvironment(
    'AUTH0_AUDIENCE',
    defaultValue: 'https://api.showvault.app',
  );
  static const apiBaseUrl = String.fromEnvironment(
    'SHOWVAULT_API_BASE_URL',
    defaultValue: 'https://api.showvault.app',
  );
  static const windowsCallbackUrl = 'showvault://callback';

  static bool get hasAuth0Client => auth0ClientId.isNotEmpty;
}
