# ShowVault native client

The shared Flutter client targets Android, iOS, macOS, and Windows. It uses Auth0 Universal Login and loads tenant-scoped recovery history from the ShowVault control plane; no preview recovery records are bundled.

## Auth0 application

Create one Auth0 application named `ShowVault Flutter` with application type `Native`. The native identity is `com.showvault.app` and the tenant domain currently used by the API is `dev-4m7moxkl7dikmtf7.us.auth0.com`.

Configure both Allowed Callback URLs and Allowed Logout URLs with:

```text
https://dev-4m7moxkl7dikmtf7.us.auth0.com/android/com.showvault.app/callback
https://dev-4m7moxkl7dikmtf7.us.auth0.com/ios/com.showvault.app/callback
com.showvault.app://dev-4m7moxkl7dikmtf7.us.auth0.com/ios/com.showvault.app/callback
https://dev-4m7moxkl7dikmtf7.us.auth0.com/macos/com.showvault.app/callback
com.showvault.app://dev-4m7moxkl7dikmtf7.us.auth0.com/macos/com.showvault.app/callback
showvault://callback
```

Windows support in the Auth0 Flutter SDK is beta and does not persist credentials. ShowVault therefore keeps the Windows session in memory. Production Windows packaging must register the `showvault` protocol handler in its installer.

## Run

Never commit an Auth0 client secret. Native clients use a public Client ID and Authorization Code + PKCE.

```bash
flutter pub get
flutter run -d macos \
  --dart-define=AUTH0_CLIENT_ID=<native-client-id> \
  --dart-define=SHOWVAULT_API_BASE_URL=https://api.showvault.app
```

Override `AUTH0_DOMAIN` and `AUTH0_AUDIENCE` only when targeting another environment. When the Android Auth0 domain differs from the repository default, also pass `-Pauth0Domain=<domain>` through Gradle.

## Verify

```bash
flutter analyze
flutter test
flutter build macos --debug
```

A macOS build requires full Xcode, not only the Command Line Tools.
