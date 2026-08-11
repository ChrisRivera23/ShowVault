# Windows packaging and controlled execution

ShowVault now has a versioned Windows packaging path and an installed-proof runner. They are ready to run on explicitly authorized controlled Windows equipment, but no Windows build or runtime evidence was produced on the current macOS host.

## Customer artifact boundary

The Windows build machine requires Windows, PowerShell 7 or newer, Flutter with Windows desktop support, Visual Studio's Desktop development with C++ workload, vcpkg, and Inno Setup 6. Set `VCPKG_ROOT` to an absolute local Windows vcpkg installation containing `vcpkg.exe` and `scripts\buildsystems\vcpkg.cmake`, then install `cpprestsdk:x64-windows`, `openssl:x64-windows`, `boost-system:x64-windows`, `boost-date-time:x64-windows`, and `boost-regex:x64-windows`. These are the exact five dependencies declared by the pinned `auth0_flutter 2.6.0` Windows manifest. ShowVault enables the toolchain before CMake's first `project()` call so the plugin can resolve them. Missing or incomplete vcpkg setup or dependencies fails before compilation. The installed customer computer needs none of those developer tools and does not install a separate ShowVault Agent or Windows service.

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

## Independent workflow-run and artifact verification

For a completed hosted run, use an authenticated GitHub CLI from `apps/showvault_app` and provide an absent output directory:

```bash
dart run tool/verify_windows_run.dart <workflow-run-id> /path/to/absent/output-directory
```

The run verifier reads the actual GitHub run metadata, requires a completed successful manual run of `Controlled Windows evidence`, fetches the workflow file at that run's exact head SHA, confirms its manual/read-only/provenance boundary and single immutable source pin, downloads only the named artifact, and invokes the artifact verifier. It then requires the checksummed artifact provenance to match the GitHub run ID, run attempt, and workflow source pin. A failed verification preserves the downloaded directory for bounded diagnosis.

For an artifact that was already downloaded through a separately trusted process, the lower-level verifier remains available:

```bash
dart run tool/verify_windows_evidence.dart /path/to/extracted/showvault-controlled-windows-evidence
```

The artifact verifier requires exactly the package and installed-proof directories, refuses linked or unexpected entries, accepts the real LF or CRLF checksum encoding, verifies both exact `SHA256SUMS` domains, validates closed JSON schemas and the report-core digest, rejects embedded paths and sensitive terms, and emits a path-free JSON summary containing artifact hashes, recorded Authenticode states, preservation results, workflow provenance, and explicit claim limitations. The checksummed provenance binds the artifact to the checked-out commit, manual workflow event, run ID, run attempt, job, runner OS/architecture, and artifact name. By itself, this lower-level command does not attest the GitHub run metadata or the workflow revision that produced the artifact.

This is independent checksum, schema, privacy, and claim-boundary verification. It validates that the Authenticode statuses are bounded values recorded by the Windows runner; it does not cryptographically establish signer trust on macOS. Distribution-signing trust still requires a separate Windows signing-policy check.

## Current evidence and blocker

As of 2026-08-10, authorized Windows run `31443483665` passed exact-source checkout, pinned Flutter installation, vcpkg-aware toolchain verification, analysis, and the complete Windows Flutter suite with 136 tests passed and 10 skipped. The normal package build entered the vcpkg toolchain but failed before installed proof because the hosted vcpkg tree did not already contain `cpprestsdk`. No provenance, checksum/cleanup, upload, or artifact was produced, and the run was not rerun.

The local correction now keeps `VCPKG_ROOT` bound to the validated hosted installation, installs the exact five `x64-windows` package specifications before Flutter verification or packaging, and verifies every requested package appears in `vcpkg list`. Static packaging coverage verifies the closed package set, triplet, ordering, redirection rejection, install failure, and missing-result failure. This package-install correction has not executed on Windows. The current host exposes only macOS and Chrome Flutter targets and has no Windows VM/device, PowerShell runtime, Wine environment, Windows SDK/MSVC toolchain, or Inno Setup compiler. Therefore the installer still has not been compiled or executed, the Auth0 native dependencies have not been resolved by a ShowVault Windows build, the URL callback has not been exercised, and no Windows artifact hash or installed evidence exists. Do not claim Windows packaging or runtime readiness until the controlled command above passes on Windows.

Host reboot, Authenticode trust/distribution signing, commercial Auth0 session expiry, provider quota exhaustion, real production-provider outage, personal-data recovery, clean-machine support range, and venue use remain separate gates.

## Manual Windows-native CI bridge

The safe default-branch strategy is specified in `docs/WINDOWS_EVIDENCE_INTEGRATION_PLAN.md`.

`.github/workflows/windows-evidence.yml` provides a manually dispatched `windows-2025` bridge when a physical controlled Windows build machine is unavailable. It grants only `contents: read`, uses pinned checkout/Flutter/upload action revisions and Flutter 3.44.8 x64, contains no secret references, has no push or pull-request trigger, and retains the synthetic artifact for 14 days.

The workflow verifies the Windows toolchain, the hosted image's absolute `VCPKG_INSTALLATION_ROOT`, the vcpkg executable/toolchain, and Inno Setup presence. It exports the validated location as `VCPKG_ROOT`, rejects later redirection, installs and verifies the exact Auth0 `x64-windows` dependency set, runs analysis and the complete Flutter suite (including the NTFS-junction test), builds the normal current-user package, executes the silent installed replacement proof, independently checks both `SHA256SUMS` files, requires callback-registration and owned-fixture cleanup, and uploads only checksummed package/evidence files.

Before checksum verification and upload, it records `windows-workflow-provenance.json` and adds that file to the installed-proof checksum set. The source commit is read from the actual checked-out Git tree rather than inferred from the workflow branch.

This workflow is published on `codex/windows-packaging` in mergeable draft PR [#25](https://github.com/ChrisRivera23/ShowVault/pull/25), but it has not been merged to the repository default branch or dispatched. PR #25 targets the nearest published ancestor and contains the accumulated post-PR #24 integration, so it must not be treated as a Windows-only diff or merged merely to expose this workflow. The branch push and normal PR CI do not authorize retargeting, marking ready, merging, or manual dispatch.

Draft PR [#26](https://github.com/ChrisRivera23/ShowVault/pull/26) is the isolated default-branch bridge, but its current published revision predates the provenance contract and pins the older source `ddfcaa6`. Do not mark it ready, merge it, or dispatch it unchanged. It must be refreshed to an explicitly approved, published, green source commit containing workflow provenance and the matching independent verifier.

A successful hosted run would establish native compiler, PowerShell, Inno Setup, NTFS test, silent installer, command-mode recovery, and cleanup evidence on the recorded runner image. It would not prove attended file-picker UX, interactive Auth0 callback behavior, a separate clean customer computer, hardware/driver compatibility, or the supported Windows range; those still require controlled attended Windows execution.
