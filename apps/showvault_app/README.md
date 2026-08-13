# ShowVault native client

The shared Flutter client targets Android, iOS, macOS, and Windows. It uses Auth0 Universal Login and loads tenant-scoped recovery history from the ShowVault control plane; no preview recovery records are bundled. On macOS and Windows, direct Scan and local Save remain available while signed out and offline.

## Local Save

Scan checks exact closed-catalog locations. Only detected user-data roots show
**Save**. The user confirms, selects the exact source and a separate vault with
native directory pickers, and can cancel while the packaged .NET local engine
captures and verifies. The UI never renders selected paths. It reports
**Verified locally**, **Cloud queued**, and **Queue attention** separately.

After **Open local vault** freshly reverifies a point, **Restore** confirms a
copy-only warning and obtains independent native consent for an existing empty
sandbox. The packaged engine publishes only `ShowVault Restored Files`, then
rehashes it and commits path-free evidence before **Restored locally** appears.
Cancel remains available before publication; interrupted or ambiguous state is
preserved as **Restore attention**. Restore remains signed-out/offline and does
not load a running application or device.

The desktop build configuration publishes the local host into a private
`local-engine` bundle directory. The host accepts only Save, vault inspection,
Restore, and in-process Cancel JSON records over standard input/output and has
no network or
arbitrary-command surface. Native build, signing, sandbox, installation, and
real-data proof remain gated and are not implied by source-level validation.

## Auth0 application

The Auth0 application `ShowVault Flutter` is registered as a Native application. Its public Client ID is configured as the app default, the native identity is `com.showvault.app`, and the tenant domain currently used by the API is `dev-4m7moxkl7dikmtf7.us.auth0.com`.

Configure both Allowed Callback URLs and Allowed Logout URLs with:

```text
https://dev-4m7moxkl7dikmtf7.us.auth0.com/android/com.showvault.app/callback
https://dev-4m7moxkl7dikmtf7.us.auth0.com/ios/com.showvault.app/callback
com.showvault.app://dev-4m7moxkl7dikmtf7.us.auth0.com/ios/com.showvault.app/callback
https://dev-4m7moxkl7dikmtf7.us.auth0.com/macos/com.showvault.app/callback
com.showvault.app://dev-4m7moxkl7dikmtf7.us.auth0.com/macos/com.showvault.app/callback
showvault://callback
```

ShowVault keeps macOS and Windows operator sessions in memory so the client never stores them in the user's personal login Keychain. Apple login uses the registered custom-scheme callback rather than requiring associated-domain entitlements. Windows support in the Auth0 Flutter SDK is beta; the runner forwards only exact `showvault://callback` activations over a current-user, single-instance channel to the Auth0 plugin. Production packaging must still register the `showvault` protocol handler, and the complete installed flow requires native Windows proof. Do not treat Windows authentication as proven until runner forwarding and installer registration pass together on Windows.

## Run

Never commit an Auth0 client secret. Native clients use a public Client ID and Authorization Code + PKCE.

```bash
flutter pub get
flutter run -d macos \
  --dart-define=SHOWVAULT_API_BASE_URL=https://api.showvault.app
```

Override `AUTH0_CLIENT_ID`, `AUTH0_DOMAIN`, and `AUTH0_AUDIENCE` only when targeting another environment. `SHOWVAULT_API_BASE_URL` must remain an HTTPS origin without credentials, a path, query, or fragment; the client rejects any other value before sending its access token. When the Android Auth0 domain differs from the repository default, also pass `-Pauth0Domain=<domain>` through Gradle.

## Verify

```bash
flutter analyze
flutter test
dotnet test ../../services/local-engine/tests/ShowVault.LocalEngine.Tests/ShowVault.LocalEngine.Tests.csproj
c++ -std=c++17 -Wall -Wextra -Werror \
  windows/runner/auth_callback_protocol.cpp \
  windows/runner/auth_callback_protocol_test.cpp \
  -o /tmp/showvault-auth-callback-protocol-test
/tmp/showvault-auth-callback-protocol-test
flutter build macos --debug
```

A macOS build requires full Xcode, not only the Command Line Tools. The portable callback test validates URI acceptance but does not replace a native Windows runner build or installed protocol activation proof.

## Personal-test macOS package

`./packaging/macos/build-app.sh /tmp/showvault-macos-personal-test` creates an
ad hoc, unnotarized personal-test app and checksum in a new absolute output
directory. It is not a distribution artifact.

The optional `--personal-beta-no-login` switch is accepted only with an
explicit loopback HTTP origin. The matching API must run in Development with
`PersonalBeta__BypassAuthentication=true` and a bounded existing
`PersonalBeta__IdentitySubject`. The server also requires the request itself to
come from loopback. Never use this bypass for customer, venue, or production
operation.
