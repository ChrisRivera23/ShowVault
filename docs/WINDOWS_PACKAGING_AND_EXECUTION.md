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

As of 2026-08-10, authorized Windows run `31446842882`, attempt 1, job `93642789076`, ran from merged `main` `b6d5aff28a310f3ccc3d7a6e1b38c2589170d5fa` and checked out immutable source `a85c7e2f4be5fef263039d8da33c30719ba5c672`. Exact-source checkout, pinned Flutter installation, and vcpkg-aware Windows/Inno Setup toolchain verification passed. The runner reported vcpkg `2026-07-27-98d7cb0cf1f4686a3e43aa5672b6230c1d56bce8` at `C:\vcpkg`.

The exact five-package installation then failed immediately because that hosted vcpkg snapshot no longer contains the `cpprestsdk` port: `C:\vcpkg\ports\cpprestsdk: error: cpprestsdk does not exist`. Flutter verification, normal packaging, installed proof, provenance, checksum/cleanup, and upload were skipped. GitHub reports zero artifacts; the run was not rerun and no artifact was retrieved.

Official vcpkg history identifies commit `9ceec72e0a30d87c469cec7d268047eb1f0424bb` as the `cpprestsdk` deindex/removal after the upstream repository was archived. ShowVault now pins its immutable dependency source to the removal commit's immediate parent, `fa9a5b330aed997a68310ed56418617b87a3b83d`. That revision contains `cpprestsdk` 2.10.19#6, OpenSSL 3.6.2, and Boost 1.91.0 system/date-time/regex ports.

The workflow still validates the hosted `VCPKG_INSTALLATION_ROOT` and rejects its redirection. It then creates an absent runner-temp checkout, fetches only the exact approved vcpkg commit, requires detached HEAD equality and every direct port manifest, bootstraps vcpkg with metrics disabled, exports only the pinned root, installs the closed five-package `x64-windows` set, verifies every result, and rejects later root redirection. Any destination, fetch, checkout, identity, port, bootstrap, executable, install, or result failure stops before Flutter verification or packaging.

Because `cpprestsdk` is archived and deindexed, this compatibility pin carries an explicit maintenance and security limitation and is not a long-term dependency-readiness claim. The current macOS host still has no Windows VM/device, PowerShell runtime, Wine environment, Windows SDK/MSVC toolchain, or Inno Setup compiler. Later controlled CI runs executed the corrected pinned checkout/bootstrap/install path and compiled both the normal and synthetic installers, but installed replacement verification still has not passed and no Windows artifact hash or complete installed evidence exists. Do not claim Windows packaging or runtime readiness until the controlled command above passes on Windows.

Exact correction source `cf76d62a867c3c3918cfb0200d429d84e9c9503a` is published on `codex/windows-packaging`. Push run `31449739175` and pull-request run `31449738769` passed all API and Flutter checks for that SHA. These Linux CI checks validate repository regression and policy coverage; they do not execute the pinned Windows vcpkg bootstrap/install path or change the Windows readiness boundary above.

A fresh bridge branch from exact prior `main` `b6d5aff28a310f3ccc3d7a6e1b38c2589170d5fa` contains commit `35b5d68b6a3bb5e25a3a240186b173a6582fba13`. It changes only `.github/workflows/windows-evidence.yml`, pins exact source `cf76d62a867c3c3918cfb0200d429d84e9c9503a`, and independently verifies at SHA-256 `d2dc4893d11baba531e4e9aada3dbe295db47801495f7c7d177959ebfb383bc9`. All 32 focused packaging/bridge/evidence tests and the diff check pass. PR #30 merged that exact bridge at current `main` `c039d5055799efe2995ab15463db19db8fa0079e`; automatic main CI run `31452046878` passed both API/Flutter jobs.

Authorized run `31452427593`, attempt 1, job `93659301883`, then executed from that exact merged `main` and immutable source. Checkout, Flutter/toolchain setup, pinned-vcpkg checkout/bootstrap and five-package installation, full Windows analysis/tests, and the normal package build passed. The installed proof rebuilt and installed its synthetic before package but failed at `run-windows-installed-proof.ps1:84` because the before application did not report successful vault preparation. The internal Dart cause is unavailable: the harness emits a generic prepare failure and the PowerShell runner replaces its captured output with a generic exception. Provenance, independent checksum/cleanup verification, and upload were skipped; GitHub reports zero artifacts. The run was not rerun, and Windows readiness remains unclaimed.

Local commits `388466e` and `2058b5e` correct the diagnostic and process-observation boundary exposed by that run without claiming an unobserved internal Dart cause. The harness now emits fixed path-free status tokens and never writes a raw exception. Prepare failures are separated into unavailable/configuration, harness prepare failure, nonzero command exit without a recognized harness category, and zero-exit missing-success-marker cases. The installed runner now launches every harness phase through a waited process with stdout/stderr redirected into its ownership-marked temporary workspace, reads the report only from those captures, and never includes raw captured output in its thrown diagnostic.

Thirty-three focused Windows packaging, harness, evidence, run-attestation, and bridge tests pass. The full Flutter suite passes 146 tests with the expected Windows-only NTFS-junction skip on macOS, analysis is clean, and the authorization verifier remains L1/L2-only. A deterministic bridge prepare/verify round trip for exact implementation head `2058b5eec11ec3e755d1906d205e57fcfb879c93` matched SHA-256 `652c86b4f4abaca6b5b5dda57d59e00e4d6bbbd618510a2216f6dbbeae74a042` byte-for-byte. The changes improve the next run's process handling and bounded failure evidence but have not been executed on Windows and do not retroactively establish which category occurred in run `31452427593`.

Exact correction source `2058b5eec11ec3e755d1906d205e57fcfb879c93` is now published as a leased fast-forward on `codex/windows-packaging`. Push CI run `31455319415` passed API job `93667779201` and Flutter job `93667779236`; pull-request CI run `31455321837` passed API job `93667786448` and Flutter job `93667786482`. This publication did not mutate the PR, refresh the evidence bridge, dispatch a workflow, retrieve an artifact, or access Windows equipment. Native execution and Windows readiness therefore remain unclaimed.

Fresh local bridge worktree `/private/tmp/showvault-windows-evidence-bridge-2058b5e.xid49L` is on branch `codex/windows-evidence-bridge-prepare-2058b5e` directly from exact `main` `c039d5055799efe2995ab15463db19db8fa0079e`. Deterministic preparation and independent pre/post-commit verification produced local commit `72ce24071784af03096574ede1f1eb2d0a8cfe7d`, pinning exact source `2058b5eec11ec3e755d1906d205e57fcfb879c93` with workflow SHA-256 `652c86b4f4abaca6b5b5dda57d59e00e4d6bbbd618510a2216f6dbbeae74a042`. The bridge changes exactly one file with one insertion and one deletion, and all 33 focused tests plus the diff check pass. No bridge push, PR mutation, merge, workflow dispatch/rerun, artifact retrieval, or equipment access occurred during local preparation.

Exact bridge commit `72ce24071784af03096574ede1f1eb2d0a8cfe7d` was subsequently pushed with the verified old-head lease from `35b5d68b6a3bb5e25a3a240186b173a6582fba13` to remote `codex/windows-evidence-bridge`. Draft PR [#31](https://github.com/ChrisRivera23/ShowVault/pull/31) targets `main`, contains the exact one-file one-line pin update, and is open, draft, clean, and mergeable at that reviewed head. Push CI run `31460402209` passed API job `93682611976` and Flutter job `93682612004`; pull-request CI run `31460424579` passed API job `93682671866` and Flutter job `93682671898`. No ready transition, merge, workflow dispatch/rerun, artifact retrieval, or equipment access occurred during publication.

After exact head/base, one-file diff, source-pin, digest, four-check, review-policy, and mergeability revalidation, PR #31 was marked ready and merged with the expected-head guard at exact reviewed head `72ce24071784af03096574ede1f1eb2d0a8cfe7d`. Current `main` is merge commit `efc7be24aab35312672c55a6f316f3bf18038a0f`; its parents are prior `main` `c039d5055799efe2995ab15463db19db8fa0079e` and exact bridge head `72ce24071784af03096574ede1f1eb2d0a8cfe7d`. The merged workflow remains byte-identical at SHA-256 `652c86b4f4abaca6b5b5dda57d59e00e4d6bbbd618510a2216f6dbbeae74a042` and pins exact product source `2058b5eec11ec3e755d1906d205e57fcfb879c93`. Automatic main CI run `31462158741` passed API job `93687694533` and Flutter job `93687694495`. No Windows workflow dispatch/rerun, artifact retrieval, or equipment access occurred during the merge gate.

Exactly one subsequently authorized manual run, [`31462319694`](https://github.com/ChrisRivera23/ShowVault/actions/runs/31462319694), attempt 1, job `93688171775`, executed from that exact merged `main` and immutable source. Exact checkout, pinned Flutter/toolchain setup, pinned-vcpkg bootstrap and five-package installation, full Windows Flutter verification, and the normal package build all passed. The installed proof rebuilt and packaged its synthetic before generation, then the installed before-app prepare process exited nonzero without emitting any recognized fixed harness status token. The corrected runner therefore stopped at `run-windows-installed-proof.ps1:97` with the bounded category `command-exit`; the hosted log does not establish the deeper internal cause, which must not be guessed. Provenance, independent checksum/cleanup verification, and upload were skipped; GitHub reports zero artifacts. The run was not rerun, no artifact was retrieved, and Windows readiness remains unclaimed.

Correction commit `5707a3c7bd05eca7e2d6344ee90817261578b715` addresses the evidence-backed diagnostic transport boundary exposed by that result. Both diagnostic harness exit paths now flush `stdout` and `stderr` before terminating with their bounded exit code, so redirected Windows captures are not abandoned immediately after a status token is written. The correction preserves nonzero failure exits and path-free fixed tokens; it does not convert the underlying prepare failure into success or claim its unavailable internal cause. The focused harness/packaging tests pass, the full Flutter suite passes 147 tests with the expected Windows-only NTFS-junction skip on macOS, and analysis is clean. The exact correction is published on `codex/windows-packaging`; push CI run `31464630084` and pull-request CI run `31464632839` passed both API and Flutter jobs. It has not been executed on Windows.

A fresh local bridge worktree directly from exact `main` `efc7be24aab35312672c55a6f316f3bf18038a0f` contains commit `daeff1f3af1ef990c678579c4005d4bf9919ab4e`. It changes only `.github/workflows/windows-evidence.yml`, replacing the immutable source pin with exact green source `5707a3c7bd05eca7e2d6344ee90817261578b715`. Deterministic preparation and independent pre/post-commit verification match SHA-256 `fa3a8e0f57b2b2e8b72f91587f45fc1d1ededff3a167a47e505e03b0576b8f06` byte-for-byte; all 34 focused harness/packaging/evidence/bridge tests and the diff check pass. This bridge has not been pushed, proposed, merged, or dispatched.

That exact bridge was subsequently pushed with the verified old-head lease from `72ce24071784af03096574ede1f1eb2d0a8cfe7d` to `codex/windows-evidence-bridge`. Draft PR [#32](https://github.com/ChrisRivera23/ShowVault/pull/32) targets `main`, contains only the one-line source-pin update, and remains draft at exact head `daeff1f3af1ef990c678579c4005d4bf9919ab4e`. Push CI run `31465834386` and pull-request CI run `31465846592` passed both API and Flutter jobs. The PR has not been marked ready or merged, and no Windows workflow was dispatched.

After exact head/base, one-file diff, source-pin, digest, four-check, review-policy, and mergeability revalidation, PR #32 was marked ready and merged with the expected-head guard at exact reviewed head `daeff1f3af1ef990c678579c4005d4bf9919ab4e`. Current `main` is merge commit `a900fd4a57392612e791acfaebdd3d09f31cd5ea`; its parents are prior `main` `efc7be24aab35312672c55a6f316f3bf18038a0f` and the exact bridge head. The merged workflow blob is byte-identical to the reviewed bridge, retains SHA-256 `fa3a8e0f57b2b2e8b72f91587f45fc1d1ededff3a167a47e505e03b0576b8f06`, and pins exact product source `5707a3c7bd05eca7e2d6344ee90817261578b715`. Automatic main CI run `31466746803` passed API job `93701117098` and Flutter job `93701117084`. No Windows workflow was dispatched.

Exactly one subsequently authorized manual run, [`31467029026`](https://github.com/ChrisRivera23/ShowVault/actions/runs/31467029026), attempt 1, job `93701958937`, executed from that exact merged `main` and immutable source. Exact checkout, pinned Flutter/toolchain setup, pinned-vcpkg bootstrap and five-package installation, full Windows Flutter verification, the normal package build, and the proof's synthetic before-package rebuild all passed. The installed before-app prepare process again exited nonzero without a recognized fixed harness token, and the runner stopped at `run-windows-installed-proof.ps1:97` with `command-exit`. Because source `5707a3c` flushes both streams before handled harness termination, this result shows that unflushed Dart output was not a sufficient explanation for the missing token; it does not establish whether the process failed before Dart entry, during native/plugin startup, or at Windows standard-stream binding. Provenance, checksum/cleanup verification, and upload were skipped; GitHub reports zero artifacts. The run was not rerun and no artifact was retrieved.

Host reboot, Authenticode trust/distribution signing, commercial Auth0 session expiry, provider quota exhaustion, real production-provider outage, personal-data recovery, clean-machine support range, and venue use remain separate gates.

## Manual Windows-native CI bridge

The safe default-branch strategy is specified in `docs/WINDOWS_EVIDENCE_INTEGRATION_PLAN.md`.

`.github/workflows/windows-evidence.yml` provides a manually dispatched `windows-2025` bridge when a physical controlled Windows build machine is unavailable. It grants only `contents: read`, uses pinned checkout/Flutter/upload action revisions and Flutter 3.44.8 x64, contains no secret references, has no push or pull-request trigger, and retains the synthetic artifact for 14 days.

The workflow verifies the Windows toolchain, the hosted image's absolute `VCPKG_INSTALLATION_ROOT`, the vcpkg executable/toolchain, and Inno Setup presence. It exports the validated location as `VCPKG_ROOT`, rejects later redirection, installs and verifies the exact Auth0 `x64-windows` dependency set, runs analysis and the complete Flutter suite (including the NTFS-junction test), builds the normal current-user package, executes the silent installed replacement proof, independently checks both `SHA256SUMS` files, requires callback-registration and owned-fixture cleanup, and uploads only checksummed package/evidence files.

Before checksum verification and upload, it records `windows-workflow-provenance.json` and adds that file to the installed-proof checksum set. The source commit is read from the actual checked-out Git tree rather than inferred from the workflow branch.

This workflow is published on `codex/windows-packaging` in mergeable draft PR [#25](https://github.com/ChrisRivera23/ShowVault/pull/25), but it has not been merged to the repository default branch or dispatched. PR #25 targets the nearest published ancestor and contains the accumulated post-PR #24 integration, so it must not be treated as a Windows-only diff or merged merely to expose this workflow. The branch push and normal PR CI do not authorize retargeting, marking ready, merging, or manual dispatch.

Draft PR [#26](https://github.com/ChrisRivera23/ShowVault/pull/26) is the isolated default-branch bridge, but its current published revision predates the provenance contract and pins the older source `ddfcaa6`. Do not mark it ready, merge it, or dispatch it unchanged. It must be refreshed to an explicitly approved, published, green source commit containing workflow provenance and the matching independent verifier.

A successful hosted run would establish native compiler, PowerShell, Inno Setup, NTFS test, silent installer, command-mode recovery, and cleanup evidence on the recorded runner image. It would not prove attended file-picker UX, interactive Auth0 callback behavior, a separate clean customer computer, hardware/driver compatibility, or the supported Windows range; those still require controlled attended Windows execution.
