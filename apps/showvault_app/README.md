# ShowVault native client

The shared Flutter client targets Android, iOS, macOS, and Windows. It uses Auth0 Universal Login and loads tenant-scoped recovery history from the ShowVault control plane; no preview recovery records are bundled.

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

Windows support in the Auth0 Flutter SDK is beta and does not persist credentials. ShowVault therefore keeps the Windows session in memory. The personal-test macOS client also keeps its operator session in memory so it never opens the operator's login Keychain; relaunching the app requires a new Auth0 sign-in. Production Windows packaging must register the `showvault` protocol handler in its installer.

## Run

Never commit an Auth0 client secret. Native clients use a public Client ID and Authorization Code + PKCE.

```bash
flutter pub get
flutter run -d macos \
  --dart-define=SHOWVAULT_API_BASE_URL=https://api.showvault.app
```

Override `AUTH0_CLIENT_ID`, `AUTH0_DOMAIN`, and `AUTH0_AUDIENCE` only when targeting another environment. When the Android Auth0 domain differs from the repository default, also pass `-Pauth0Domain=<domain>` through Gradle.

## Verify

```bash
flutter analyze
flutter test
flutter build macos --debug
```

A macOS build requires full Xcode, not only the Command Line Tools.

## Build a macOS personal-test artifact

The packaging script creates a release-mode `ShowVault.app`, a ZIP suitable for transfer to another personal Mac, and a SHA-256 checksum. The output directory must be an absolute path that does not already exist:

```bash
./packaging/macos/build-app.sh /tmp/showvault-macos-personal-test
```

The default artifact connects to `https://api.showvault.app`. A controlled local build may use an explicit loopback HTTP endpoint:

```bash
./packaging/macos/build-app.sh \
  /tmp/showvault-macos-local-test \
  http://127.0.0.1:5000
```

For the attended personal beta only, the visible login can be omitted. This
mode works exclusively with a loopback API running in the Development
environment with `PersonalBeta__BypassAuthentication=true` and an explicitly
configured `PersonalBeta__IdentitySubject`:

```bash
./packaging/macos/build-app.sh \
  /tmp/showvault-macos-local-no-login \
  http://127.0.0.1:5000 \
  --personal-beta-no-login
```

The no-login switch is rejected for non-loopback endpoints. The API also
rejects it outside Development, from non-loopback clients, or without an
explicit identity subject. It is test scaffolding and must not be used for a
customer or production build.

Every other non-HTTPS endpoint is rejected. The endpoint is build configuration, not a venue identity: organizations and venues come from the authenticated control plane in normal builds or the explicitly selected existing test identity in the loopback personal beta. No venue name, address, equipment, path, credential, or other private data is packaged. Native Auth0 clients use the repository's public client ID with Authorization Code + PKCE; no client secret is accepted or embedded.

These artifacts are intentionally for personal-equipment testing. They are not distribution-signed or notarized and must not be installed at a venue. Controlled application replacement now preserves and rehydrates the external local vault; clean-machine installation, rollback, distribution signing, and notarization remain required by [`../../docs/PROTOTYPE_READINESS.md`](../../docs/PROTOTYPE_READINESS.md).

## Run the installed synthetic resilience matrix

The repository-owned matrix builds a separately gated release app and executes safe API/storage outage, restart/resume, tamper, incomplete-object, conflicting-chunk, and restore-failure scenarios against disposable infrastructure:

```bash
./tool/run-resilience-matrix.sh /private/tmp/showvault-resilience-matrix
```

This special artifact is loopback-only test scaffolding and must not be distributed. The runner emits a path-free evidence report and removes its synthetic sandbox workspace and disposable volumes. See [`../../docs/INSTALLED_RESILIENCE_MATRIX.md`](../../docs/INSTALLED_RESILIENCE_MATRIX.md).

## Run the installed upgrade and diagnostic proof

The upgrade runner builds two distinct release apps, replaces the installed synthetic app between them, removes the synthetic source, and verifies that the external vault, manifest, queue journal, and restore evidence rehydrate intact. It also exercises the path-free local support diagnostic:

```bash
./tool/run-upgrade-diagnostic-proof.sh \
  /private/tmp/showvault-upgrade-diagnostic-proof
```

The output directory must be absolute and absent. This command is synthetic macOS test scaffolding, not an installer, notarization, Windows, or rollback claim. See [`../../docs/UPGRADE_AND_SUPPORT_DIAGNOSTICS.md`](../../docs/UPGRADE_AND_SUPPORT_DIAGNOSTICS.md).

## Build the Windows package

On a Windows build machine with PowerShell 7, Flutter Windows support, Visual Studio C++ tooling, and Inno Setup 6:

```powershell
pwsh -File .\packaging\windows\build-app.ps1 `
  -OutputDirectory C:\ShowVaultArtifacts\release
```

The current-user installer registers only the `showvault://` authentication callback, replaces only application files during upgrade, and retains the operator-selected external vault during upgrade or uninstall. The package includes a portable ZIP, path-free package manifest, observed signature status, and SHA-256 checksums.

The controlled installed proof is ready at `tool\run-windows-installed-proof.ps1`. It must be executed on authorized Windows equipment before Windows readiness is claimed. See [`../../docs/WINDOWS_PACKAGING_AND_EXECUTION.md`](../../docs/WINDOWS_PACKAGING_AND_EXECUTION.md).

A manual-only Windows Server 2025 workflow is also versioned at `.github/workflows/windows-evidence.yml`. It has read-only repository permission, pinned actions, no secret usage, and no automatic trigger. It has not been pushed or executed; doing so requires separate authorization and still does not replace attended picker/Auth0 validation on a controlled Windows computer.
