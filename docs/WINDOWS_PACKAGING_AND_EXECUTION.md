# Windows packaging and controlled execution

ShowVault now has a versioned Windows packaging path and an installed-proof runner. They are ready to run on explicitly authorized controlled Windows equipment, but no Windows build or runtime evidence was produced on the current macOS host.

## Customer artifact boundary

The Windows build machine requires Windows, PowerShell 7 or newer, Flutter with Windows desktop support, Visual Studio's Desktop development with C++ workload, and Inno Setup 6. The installed customer computer needs none of those developer tools and does not install a separate ShowVault Agent or Windows service.

From `apps/showvault_app`, build a normal package into an absent local-drive directory:

```powershell
pwsh -File .\packaging\windows\build-app.ps1 `
  -OutputDirectory C:\ShowVaultArtifacts\release
```

The script performs a clean Flutter x64 release build and verifies that `ShowVault.exe`, `flutter_windows.dll`, `data\flutter_assets`, and `data\app.so` exist. It produces:

- `ShowVault-0.1.0-1-windows-x64-setup.exe`;
- `ShowVault-0.1.0-1-windows-x64.zip` containing the complete portable deployment;
- `windows-package-manifest.json` with bounded, path-free package metadata and observed Authenticode status; and
- `SHA256SUMS` for the installer, ZIP, and package manifest.

The version comes from `pubspec.yaml`. The script rejects an existing output, relative or UNC output, non-HTTPS control-plane origins other than controlled loopback HTTP, a no-login build against a non-loopback endpoint, and incomplete Flutter output.

## Installer behavior

The Inno Setup installer is current-user scoped and does not request administrator privileges. It installs under the current user's local application-data programs directory, creates optional user shortcuts, and registers only the `showvault` URL scheme under `HKCU\Software\Classes`. The registered command forwards `showvault://callback` to `ShowVault.exe` for the Windows Auth0 flow.

An upgrade replaces only files beneath `{app}`. Neither upgrade nor uninstall identifies, traverses, or deletes the operator-selected ShowVault Pro vault. No installer section deletes external data, source data, credentials, hosted copies, or neighboring files. Full local-data removal remains a separate attended procedure described in `UPGRADE_AND_SUPPORT_DIAGNOSTICS.md`.

The package records its actual Authenticode status. The current implementation does not embed a certificate, private key, token, password, venue identity, endpoint inventory, or source/vault path. Distribution signing remains a separate release gate.

## Windows local-path boundary

Selected source, vault, and Restore directories must resolve to a non-root absolute local drive path. The access boundary rejects:

- drive-relative and root-relative paths;
- UNC/network shares and extended/device namespaces;
- `.` or `..` traversal segments;
- alternate-data-stream syntax;
- empty segments and trailing dot/space aliases; and
- filesystem links, junctions, or other substituted entries.

Canonical comparisons and containment are case-insensitive and separator-normalized, with segment boundaries preventing `C:\VaultSibling` from being treated as a child of `C:\Vault`. The diagnostic privacy filter rejects standalone or embedded Windows drive paths, UNC paths, Unix paths, and `file://` values.

Pure path-policy tests run on every development host. The real junction test is versioned but intentionally skips outside Windows because NTFS junction semantics require Windows execution.

## Controlled installed proof

On an isolated controlled Windows user where the `showvault` callback scheme is not already registered, run:

```powershell
pwsh -File .\tool\run-windows-installed-proof.ps1 `
  -OutputDirectory C:\ShowVaultEvidence\installed-proof
```

The runner refuses to disturb an existing callback registration. It uses an ownership-marked temporary directory, compiles before/after installers, installs the before artifact, creates the synthetic local recovery point and retry/synchronized journal, performs Restore, generates a diagnostic, removes the synthetic source, installs the after artifact over the same application directory, and validates source-free vault rehydration. It then exports path-free reports, execution/signature metadata, and checksums; asks the harness to remove its owned synthetic vault; uninstalls the application; and removes only its marker-owned temporary directory.

Successful evidence must show:

- Windows scope and OS version/architecture without computer or user identity;
- distinct before/after artifacts;
- application replacement with the external vault retained;
- immutable recovery point and independent-manifest verification;
- retry attempt 2 and four append-only queue events;
- retained Restore evidence and explicit bounded diagnostics;
- source-free rehydration;
- observed executable/installer Authenticode states;
- exact artifact SHA-256 values; and
- removal of the synthetic fixture, installed application, callback registration, and owned workspace.

## Current evidence and blocker

As of 2026-08-10, Flutter analysis passes and 99 Flutter tests pass with one Windows-only NTFS-junction test skipped on macOS. Static packaging tests verify the current-user protocol registration, complete Flutter deployment checks, checksum production, external-vault retention rule, marker-scoped cleanup, manual workflow boundary, and absence of installer-driven vault deletion.

The current host exposes only macOS and Chrome Flutter targets and has no Windows VM/device, PowerShell runtime, Wine environment, Windows SDK/MSVC toolchain, or Inno Setup compiler. Therefore the installer has not been compiled or executed, PowerShell/Inno syntax has not been validated by their native engines, the URL callback has not been exercised, the junction test has not run, and no Windows artifact hash or installed evidence exists. Do not claim Windows packaging or runtime readiness until the controlled command above passes on Windows.

Host reboot, Authenticode trust/distribution signing, commercial Auth0 session expiry, provider quota exhaustion, real production-provider outage, personal-data recovery, clean-machine support range, and venue use remain separate gates.

## Manual Windows-native CI bridge

`.github/workflows/windows-evidence.yml` provides a manually dispatched `windows-2025` bridge when a physical controlled Windows build machine is unavailable. It grants only `contents: read`, uses pinned checkout/Flutter/upload action revisions and Flutter 3.44.8 x64, contains no secret references, has no push or pull-request trigger, and retains the synthetic artifact for 14 days.

The workflow verifies the Windows toolchain and Inno Setup presence, runs analysis and the complete Flutter suite (including the NTFS-junction test), builds the normal current-user package, executes the silent installed replacement proof, independently checks both `SHA256SUMS` files, requires callback-registration and owned-fixture cleanup, and uploads only checksummed package/evidence files.

This workflow is committed locally but has not been pushed, merged to the repository default branch, or dispatched. Adding the workflow does not authorize those external actions. A successful hosted run would establish native compiler, PowerShell, Inno Setup, NTFS test, silent installer, command-mode recovery, and cleanup evidence on the recorded runner image. It would not prove attended file-picker UX, interactive Auth0 callback behavior, a separate clean customer computer, hardware/driver compatibility, or the supported Windows range; those still require controlled attended Windows execution.
