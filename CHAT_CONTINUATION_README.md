# ShowVault active continuation handoff

Read this file completely at the start of a new ShowVault development chat. It is the concise authority for the current state and next task. Repository code, contracts, tests, migrations, ADRs, and Git history remain authoritative when more specific.

## Product direction

ShowVault is a recovery-first venue-resilience platform:

**Scan → Backup → Verify → Restore**

The customer desktop experience must be simple:

**Install → Scan this computer → Sign in for cloud service**

There is no customer-facing Agent installation, enrollment code, service setup, or personal-Keychain workflow. The product is local-first: it creates immutable recovery points, manifests, verification evidence, and a durable upload queue in a configurable local ShowVault Pro vault. The control plane receives only the metadata required for hosted coordination; secrets remain separately protected.

`docs/LOCAL_FIRST_PRODUCT_BIBLE.md` is the current product authority. It supersedes the former direct-to-cloud/no-local-persistence direction in older commits and handoffs.

The implementation remains venue-neutral and cross-platform. LIV nightclub is the intended first venue deployment, not a source of build-time assumptions or current test data. Use only the Product Owner's Mac and other explicitly authorized controlled equipment until the readiness gates pass.

## Current repository state

- Repository: `/Users/infamous/Documents/ChatGPT/showvault`
- Branch: `codex/windows-packaging`
- Local-first vault foundation commit: `bc53f4b feat: establish local-first vault foundation`
- Offline desktop Save commit: `85b3e92 feat: save desktop recovery points offline`
- Desktop permission/rehydration commit: `07e6e62 feat: authorize and rehydrate local vaults`
- Durable queue synchronization commit: `f016ad1 feat: synchronize durable local queue`
- Attended local restore commit: `36fcda9 feat: restore verified local recovery points`
- Authenticated hosted synchronization commit: `5f05f44 feat: synchronize through authenticated hosted storage`
- Installed-drill starting handoff commit: `e980165 docs: hand off installed hosted sync drill`
- Deployable object-storage implementation commit: `c965719 feat: add deployable object storage sync`
- Prototype storage runbook commit: `69b83ab docs: document prototype storage operations`
- Installed resilience harness commit: `d744c03 feat: automate installed resilience matrix`
- Installed resilience evidence commit: `75a2586 docs: record installed resilience evidence`
- Upgrade preservation and support diagnostics implementation commit: `237f076 feat: preserve upgrades and generate support diagnostics`
- Upgrade evidence and operating-boundary documentation commit: `b9f0824 docs: record upgrade diagnostic evidence`
- Windows packaging and safety implementation commit: `58ad46a feat: package the Windows local-first client`
- Windows package execution-gate documentation commit: `5c7ade7 docs: define Windows package execution gate`
- Manual Windows-native evidence workflow commit: `6fdccca ci: add controlled Windows evidence workflow`
- Published CI correction commit: `ddfcaa6 fix: preserve configured package storage in CI`
- Downloaded Windows evidence verifier commit: `a1a69eb feat: verify downloaded Windows evidence`
- Workflow-provenance binding commit: `0644cb1 feat: bind Windows evidence to workflow provenance`
- GitHub run/workflow attestation commit: `7592fbe feat: attest downloaded Windows workflow runs`
- Deterministic Windows evidence-bridge preparation commit: `a927c20 feat: prepare deterministic Windows evidence bridge`
- Deterministic Windows evidence-bridge verification commit: `1ce2efc feat: verify deterministic Windows evidence bridge`
- Product-integration decomposition plan commit: `626e88d docs: plan local-first product integration`
- Local-first integration overlap audit commit: `a1c3c83 docs: audit local-first integration overlaps`
- Direct-scan integration milestone manifest commit: `d0c69b3 docs: manifest direct scan integration milestone`
- Offline-Save integration milestone manifest commit: `e7635fa docs: manifest offline save integration milestone`
- Sync/Restore integration milestone manifest commit: `094b7a6 docs: manifest sync restore integration milestone`
- Object-storage integration milestone manifest commit: `b1d03f6 docs: manifest object storage integration milestone`
- Resilience/diagnostics integration milestone manifest commit: `15a7b4f docs: manifest resilience diagnostics milestone`
- Windows-evidence integration milestone manifest commit: `b28de57 docs: manifest Windows evidence integration milestone`
- Cross-manifest consistency audit commit: `8e76d1d docs: audit integration manifest consistency`
- Consolidated integration execution checklist commit: `d0b07a5 docs: consolidate integration execution checklist`
- Local-first integration preflight verifier commit: `13afb8d feat: verify local-first integration topology`
- PR #3–#24 local dependency ledger commit: `7e85e92 docs: record local PR dependency ledger`
- PR dependency topology verifier commit: `1b1dfbf feat: verify local PR dependency topology`
- Local authorization readiness matrix commit: `b2c6ddd docs: map local authorization readiness`
- Combined local integration preflight commit: `78c6c8d feat: combine local integration preflights`
- Authorization-boundary verifier commit: `516175d feat: verify local authorization boundaries`
- Published provenance-protected product head: `f3b7a1deeef76004fbc7c65f6256e6b24043916d docs: hand off combined local safety preflights`
- Windows policy line-ending fix: `2ebfe45 fix: normalize Windows policy text inputs`
- Windows restore-target canonicalization fix: `05ed93d fix: canonicalize Windows restore targets`
- Sandbox-safe selected-target restore commit: `a62649f fix: restore safely into selected sandbox folders`
- Immediate cloud-status refresh commit: `a7eee0d fix: refresh synchronized recovery status`
- Direct-scan commit: `3ed4bdc feat: scan computers directly without agent enrollment`
- Navigation/no-login beta commit: `eea1d45 feat: restore navigation and add guarded no-login beta`
- Corrected published product head: `6d6f47ac3b32041e56238060b8b2cd8e13485a2d docs: hand off Windows native test fixes`
- Replacement Windows evidence bridge commit: `add59af3c5f3f910259f20d83306ce355737035c ci: refresh Windows evidence source pin`
- Replacement bridge merge commit on `main`: `23ec1feda998fda1d1402a8f547bd7400e31f7d0 Merge pull request #27 from ChrisRivera23/codex/windows-evidence-bridge`
- Windows Auth0 native dependency correction: `5046ad1d0b47e9a44adab901c5c75cac1249b840 fix: configure Windows Auth0 native dependencies`
- Published green vcpkg-corrected product head: `ddde50cf96bf5cd86bc3aa578b893a720b4f2840 docs: hand off Windows dependency correction`
- Local vcpkg replacement bridge commit: `5a737d23e2d8a3bcec8ef034ea88eb1fd68169b9 ci: refresh Windows evidence source pin`
- Vcpkg replacement bridge merge commit on `main`: `43242c03bb6b8ee76eec3fc9debdd6f2c6daf850 Merge pull request #28 from ChrisRivera23/codex/windows-evidence-bridge`
- Windows native-package installation correction: `5c2b24c876eec0db7bc4359aedff57259a8f46f0 fix: install Windows Auth0 native packages`
- Published green native-package product head: `a85c7e2f4be5fef263039d8da33c30719ba5c672 docs: hand off Windows native package correction`
- Local native-package replacement bridge commit: `a307e5499d3396027bcc67fc4856ff236542fb31 ci: refresh Windows evidence source pin`
- Published native-package bridge head: `a307e5499d3396027bcc67fc4856ff236542fb31 ci: refresh Windows evidence source pin`
- Draft native-package bridge PR: `#29` (`https://github.com/ChrisRivera23/ShowVault/pull/29`), exact head `a307e5499d3396027bcc67fc4856ff236542fb31`, targeting `main`
- Green native-package bridge CI: push run `31446160889` and pull-request run `31446174726`
- Native-package bridge merge commit on `main`: `b6d5aff28a310f3ccc3d7a6e1b38c2589170d5fa Merge pull request #29 from ChrisRivera23/codex/windows-evidence-bridge`
- Expected worktree after the handoff commit: clean except intentionally untracked `NEXT_CONVERSATION.md`
- Sequential catalog expansion remains paused while prototype readiness advances.

## Completed outcome

- The full product sidebar is restored: Dashboard, Venues, Devices, Discovery, Backups, Verification, Recovery, Digital Twin, Plugins, and Settings. The Product Owner will decide later what to remove or rearrange.
- Agent installation, one-time enrollment, Venue Agent selectors, and Agent recovery-workflow controls remain excluded from the customer dashboard.
- The attended personal beta omits login. Its bypass requires an explicit app build flag, a loopback HTTP API, a Development server, an explicit server flag and existing test identity, and a loopback client.
- Normal/production builds still use Auth0. macOS and Windows sessions are held in application memory only, and ShowVault does not open the macOS login Keychain.
- `LocalCatalogScanner` checks only exact catalog-defined candidates. It does not enumerate installed applications, directories, disks, networks, machine identity, or file contents.
- The current direct beta registry checks Resolume Arena and Serato DJ Pro application/user-data candidates on macOS and Windows.
- The app submits only opaque candidate keys to the authenticated manager-only `POST /api/v1/organizations/{organizationId}/venues/{venueId}/computer-scans` endpoint.
- The server independently allowlists every accepted key and maps it to bounded product/type/evidence metadata. Unknown keys, paths, and oversized requests are rejected.
- Direct scan headers and candidates are stored in the cloud database. An empty newer scan correctly supersedes older results.
- Direct detections appear as **Detected** and cannot enter the legacy Agent approval/backup controls.
- Legacy Agent protocol 1.21 remains in the repository only as compatibility infrastructure; it is not part of the customer desktop onboarding flow.
- Product direction now requires a local ShowVault Pro vault, offline backup and verification, local manifests, and durable verified-only cloud synchronization state.
- The local recovery foundation creates the configurable canonical vault folders at startup before network enrollment, and stores its durable SQLite workflow/upload queue in `Upload Queue` by default.
- Default recovery points are immutable and named `Backups/<parent>/<UTC timestamp>__<SHA-256 recovery-point ID>`; legacy configured package directories remain compatible.
- A passing local structural/cryptographic verification creates exactly one idempotent queued upload job. Failed or unverified packages are not queued; normal signed-in builds now synchronize through the authenticated hosted API.
- The Flutter dashboard keeps Scan and Save usable while signed out or the API is unavailable. Cloud submission is best-effort and cannot erase local findings.
- An explicit desktop Save resolves only an opaque allowlisted `UserDataRoot` key after confirmation, rejects links and unsafe entries, enforces count/size/timeout/cancellation/mutation rules, streams and independently verifies SHA-256 content, publishes atomically, and writes a JSON upload-queue job only after verification.
- Native macOS and Windows directory selectors now sit behind one Dart access contract. Source authorization requires an exact canonical match to the catalog root; vault authorization is configurable and session-scoped.
- **Open local vault** performs a bounded inspection of ShowVault-owned manifests, package manifests, and queue records, verifies manifest identity and equality, and restores independent local/cloud status without rescanning a source.
- The desktop synchronization executor reverifies exact package content, filters local paths from its remote manifest, resumes bounded chunks from durable remote length, uses an append-only local state journal with capped retry/backoff, remotely verifies every checksum, and publishes idempotently through either the authenticated hosted API or the explicitly synthetic folder substitute.
- Normal signed-in builds hold the Auth0 access token only in memory and construct the hosted transport only after an organization and venue load. The server requires manager/administrator/owner membership, independently validates the exact manifest and approved catalog metadata, and derives all storage paths from authorized tenant GUIDs and bounded identities.
- The hosted `begin` operation freezes the validated manifest before chunks are accepted. Duplicate chunks and concurrent completion are idempotent; gaps, conflicting bytes, wrong checksums, extra files, links, unsafe metadata, and cross-tenant access are rejected.
- Production API startup now requires the S3-compatible provider and fails closed on missing/unsafe storage configuration. The configured server-owned filesystem provider and disabled provider are Development-only.
- Hosted manifests and create-only chunks use server-derived tenant/package keys; logical paths are represented only by SHA-256 key segments. Commit relists and rehashes the package and conditionally creates `receipt.json` last as the sole completion marker.
- A pinned multi-stage API image, PostgreSQL service, one-shot migration job, workload-identity-compatible configuration, liveness/readiness endpoints, disposable MinIO override, synthetic hosted-sync smoke command, and operator runbook are versioned under `infra/` and `docs/DEPLOYABLE_PROTOTYPE_STORAGE.md`.
- A compile-time-gated macOS resilience command mode runs only with an opaque sandbox fixture identity and loopback API. Normal builds keep it disabled. The versioned runner launches the copied release executable as a new process for each phase; it does not use `flutter run` or a separate customer Agent.
- The installed matrix now automates API/storage outage and recovery, durable partial upload and cross-process resume, idempotent completion, verified Restore, source mutation, local/remote tamper, incomplete/conflicting remote chunks, non-empty targets, and interrupted Restore. Its report is path-free and checksummed.
- **Create support diagnostic** is an explicit confirmed operator action after opening a vault. It validates bounded ShowVault-owned manifests, every queue-state event, and restore evidence, then writes a checksummed `showvault.support-diagnostic.v1` report under `Reports/Diagnostics` without reading package contents or recovery sources.
- Diagnostics include only versions, timestamps, counts, opaque package/candidate identities, statuses, bounded error categories, and integrity results. They exclude raw errors, credentials, tokens, contents, exact paths, host identity, and unrestricted filesystem/network/application inventory.
- Linked, substituted, malformed, oversized, wrongly identified, or checksum-invalid manifest/queue/report entries fail before diagnostic publication. Linked diagnostic destinations cannot redirect a write outside the authorized vault.
- A two-artifact installed macOS proof replaces a before app with an independently compiled after app. With the synthetic source deleted, the after app rehydrated the same immutable recovery point, independent manifest, synchronized attempt 2/four-event journal, and one restore-evidence record, then generated a second diagnostic.
- App replacement and ordinary app removal retain the selected external vault. Full local-data removal is a distinct attended procedure and is not currently an in-app destructive control. Clean-machine reinstall and rollback execution remain unproven.
- The Windows x64 client now builds as `ShowVault.exe`. A PowerShell 7 packaging script verifies the complete Flutter deployment, and Inno Setup produces a current-user installer, portable ZIP, bounded path-free package manifest, observed Authenticode status, and SHA-256 checksums.
- The Windows installer registers only the `showvault://` authentication callback under the current user. Upgrade replaces only `{app}`; upgrade and uninstall retain the external ShowVault Pro vault and install no customer Agent or service.
- Windows selected-folder policy rejects relative, drive-root, UNC/network, extended/device, traversal, alternate-stream, trailing-alias, and linked/substituted paths. Canonical comparison and containment are case-insensitive and segment-bounded. Diagnostic privacy rejects embedded drive, UNC, Unix, and `file://` paths.
- A marker-scoped installed-proof runner is ready to compile/install before and after packages, exercise synthetic Save/retry/sync/Restore/diagnostic/source-removal, verify source-free rehydration, export checksummed path-free evidence, clean the fixture, uninstall the app, and remove only its owned workspace.
- The local Mac still has no Windows Flutter target/VM, PowerShell runtime, Wine, MSVC/Windows SDK, or Inno Setup. The authorized GitHub Windows runner executed the native Flutter toolchain and analysis, but packaging and installed proof did not run because the test stage failed. Windows readiness remains unclaimed.
- A manual-only `windows-2025` workflow runs the complete native test/package/installed-proof/checksum/cleanup sequence with read-only repository permission, pinned action revisions, Flutter 3.44.8 x64, no secrets, no automatic trigger, and 14-day synthetic artifact retention. The isolated one-file bridge was merged through PR #26 and now exists on `main` at merge commit `b211804c1d01f93e0d64b7e766637892fd0bb7fe`.
- PR #25 targets the nearest published ancestor, `codex/yamaha-dme5-dme3`, because the intervening branch stack exists only locally. GitHub reports 287 accumulated commits and 293 changed files, so it is an integration draft rather than a Windows-only review.
- The first PR CI run exposed a Linux-runner defect: legacy Agent package-directory mode unnecessarily resolved the unavailable Documents folder. Commit `ddfcaa6` makes local-vault construction conditional while retaining the fail-closed default-vault behavior. All 429 Agent tests pass locally, and all four push/pull-request API and Flutter checks on the corrected head pass.
- The cross-platform downloaded-evidence verifier independently requires the exact two artifact directories, refuses links and unlisted files, accepts real PowerShell CRLF or LF checksum files, verifies both exact checksum sets, enforces closed package/metadata/report/provenance schemas, verifies the report-core digest, rejects path/sensitive-value leakage, and emits only bounded hashes, statuses, workflow identity, results, and limitations. Seven focused positive/adversarial tests pass. Recorded Authenticode statuses are validated as bounded evidence; signer trust remains a separate Windows check.
- Product commit `f3b7a1deeef76004fbc7c65f6256e6b24043916d` was published and all four product CI checks passed. The deterministic bridge commit `5586b25408df9fb36889b71b565fb5517de2d69c` pins that exact source, has workflow digest `e2bff6150fc129c78f16663bb2281a7c48fe3154e4008a55712ecf1575320131`, and was merged through PR #26. The merge's second parent is the exact reviewed bridge commit.
- Exactly one authorized manual run was dispatched: run `31437839213`, attempt 1, job `93615920797`, on `main` commit `b211804c1d01f93e0d64b7e766637892fd0bb7fe`, checking out immutable source `f3b7a1deeef76004fbc7c65f6256e6b24043916d`. Toolchain setup and analysis passed; Flutter tests reported 117 passed, 15 failed, and 10 skipped. The run stopped before packaging, proof, provenance, checksums, or upload, produced zero artifacts, and was not rerun.
- Corrected product source `6d6f47ac3b32041e56238060b8b2cd8e13485a2d` was published with all four push/PR API and Flutter checks successful. PR #27 then passed all four checks and was marked ready and merged only at exact reviewed bridge head `add59af3c5f3f910259f20d83306ce355737035c`.
- The resulting `main` is `23ec1feda998fda1d1402a8f547bd7400e31f7d0`; its parents are prior `main` `b211804c1d01f93e0d64b7e766637892fd0bb7fe` and exact bridge head `add59af3c5f3f910259f20d83306ce355737035c`. The merged workflow blob is byte-identical to the deterministic candidate, pins exact product source `6d6f47ac3b32041e56238060b8b2cd8e13485a2d`, and retains SHA-256 `8dedff791fb292b5a3738a72945df6067a5f479cd3330f1bfb4a8eb19047e8f2`.
- Exactly one replacement Windows workflow run was dispatched after the replacement bridge was merged: run `31441006486`, attempt 1, job `93625511666`, on `main` `23ec1feda998fda1d1402a8f547bd7400e31f7d0`, checking out exact source `6d6f47ac3b32041e56238060b8b2cd8e13485a2d`. Checkout, pinned Flutter installation, Windows toolchain verification, analysis, and the complete Flutter suite passed with 135 tests passed and 10 skipped.
- Run `31441006486` failed during the normal package build before installed proof. `auth0_flutter 2.6.0` called `find_package(cpprestsdk CONFIG REQUIRED)`, but ShowVault's top-level `windows/CMakeLists.txt` did not enable the required vcpkg toolchain before `project()`, so CMake could not find `cpprestsdkConfig.cmake`. Installed proof, provenance, checksum/cleanup, and upload were skipped; GitHub reports zero artifacts. The run was not rerun.
- The workflow toolchain preflight checked Flutter and Inno Setup but did not check the vcpkg/Auth0 native dependency boundary. Windows readiness remains unclaimed.
- Commit `5046ad1` now requires an absolute local `VCPKG_ROOT` with `vcpkg.exe` and its CMake toolchain, selects that toolchain before the app's first CMake `project()` call, and makes the hosted workflow validate the runner's `VCPKG_INSTALLATION_ROOT` before exporting it as `VCPKG_ROOT` for later steps. Missing, malformed, incomplete, or failing vcpkg setup stops before compilation.
- The correction preserves normal Auth0 behavior, the manual/read-only workflow boundary, the exact three pinned actions, and deterministic immutable-source bridge preparation. Focused packaging/bridge/run tests passed 25; Flutter analysis is clean; the full local suite passed 145 with one Windows-only junction test skipped on macOS. A post-commit bridge prepare/verify round trip pinned exact commit `5046ad1d0b47e9a44adab901c5c75cac1249b840` and reproduced digest `0c7dbc82637ec64c9036d575d24e0a897c985f2db1b9cd4b4b1f55784ab1dcd2` byte-for-byte.
- The macOS host has no `cmake`, PowerShell, Windows compiler/SDK, or Inno Setup, so the corrected native dependency resolution and package build remain unexecuted. No push, bridge mutation, dispatch, artifact retrieval, or equipment access occurred during this local correction.
- Exact product head `ddde50cf96bf5cd86bc3aa578b893a720b4f2840` was subsequently pushed as a fast-forward to `codex/windows-packaging`. Push CI run `31442451879` and pull-request CI run `31442455228` both succeeded; all four API/Flutter jobs are green for that exact SHA.
- A fresh local worktree at `/private/tmp/showvault-windows-evidence-bridge-ddde50c` uses branch `codex/windows-evidence-bridge-vcpkg-ddde50c` from unchanged `origin/main` `23ec1feda998fda1d1402a8f547bd7400e31f7d0`. Deterministic preparation and independent verification produced exact one-file bridge commit `5a737d23e2d8a3bcec8ef034ea88eb1fd68169b9`, pinning product source `ddde50cf96bf5cd86bc3aa578b893a720b4f2840` with workflow SHA-256 `d30609fe01afe7cbd3c62d99ff2ecaddd05aaeaf2023235a512bd2308c579307`.
- The bridge diff contains one file with 19 insertions and one deletion: it replaces the immutable source pin and carries the reviewed vcpkg preflight. At completion of the local preparation gate, no remote bridge state had changed and no workflow was dispatched.
- Exact bridge commit `5a737d23e2d8a3bcec8ef034ea88eb1fd68169b9` was then pushed as a fast-forward to `codex/windows-evidence-bridge`. Draft PR #28, https://github.com/ChrisRivera23/ShowVault/pull/28, targets `main`, remains open/draft/mergeable, and contains exactly the one workflow file with 19 insertions and one deletion.
- PR #28's description records the exact green source, bridge commit/digest, both failed zero-artifact runs, and separate merge/dispatch/artifact/equipment gates. Push CI run `31442875281` and pull-request CI run `31442897754` both succeeded; all four API/Flutter jobs are green for exact bridge head `5a737d23e2d8a3bcec8ef034ea88eb1fd68169b9`.
- At completion of the publication gate, PR #28 remained draft and no new Windows workflow dispatch had occurred.
- After exact live-state, source-pin, digest, one-file-diff, and four-check revalidation, PR #28 was marked ready and merged only at reviewed head `5a737d23e2d8a3bcec8ef034ea88eb1fd68169b9`.
- The resulting `main` is `43242c03bb6b8ee76eec3fc9debdd6f2c6daf850`; its parents are prior `main` `23ec1feda998fda1d1402a8f547bd7400e31f7d0` and exact bridge head `5a737d23e2d8a3bcec8ef034ea88eb1fd68169b9`. Main's workflow blob is byte-identical to the deterministic candidate, pins exact green source `ddde50cf96bf5cd86bc3aa578b893a720b4f2840`, and retains SHA-256 `d30609fe01afe7cbd3c62d99ff2ecaddd05aaeaf2023235a512bd2308c579307`.
- Exactly one vcpkg-corrected Windows workflow run was dispatched: run `31443483665`, attempt 1, job `93632804740`, on `main` `43242c03bb6b8ee76eec3fc9debdd6f2c6daf850`, checking out exact green source `ddde50cf96bf5cd86bc3aa578b893a720b4f2840`. Checkout, pinned Flutter installation, Windows toolchain verification, analysis, and the complete Flutter suite passed with 136 tests passed and 10 skipped. The runner reported Windows 11 24H2, Visual Studio Enterprise 2026 18.8.2, and vcpkg `2026-07-27-98d7cb0cf1f4686a3e43aa5672b6230c1d56bce8` at `C:\vcpkg`.
- Run `31443483665` failed during the normal current-user package build. CMake successfully entered `C:\vcpkg\scripts\buildsystems\vcpkg.cmake`, but the runner's vcpkg tree did not contain an installed `cpprestsdk` package configuration, so Auth0's `find_package(cpprestsdk CONFIG REQUIRED)` still failed. The workflow validated the vcpkg executable/toolchain but did not install the required native packages.
- Installed replacement proof, workflow provenance, checksum/cleanup, and upload were skipped. GitHub reports zero artifacts. The run was not rerun, and no artifact was retrieved. Windows readiness remains unclaimed.
- Commit `5c2b24c` adds a dedicated pre-build workflow step that keeps `VCPKG_ROOT` equal to the validated hosted installation, installs the pinned Auth0 2.6.0 Windows dependency set as exact `x64-windows` package specifications (`cpprestsdk`, OpenSSL, and the three required Boost components), fails on a nonzero install, and independently requires every specification in `vcpkg list`. The workflow still has only the manual trigger, read-only repository permission, three commit-pinned actions, and the existing artifact policy.
- Static coverage requires the closed five-package set and triplet, redirection rejection, install/result failures, and ordering before Flutter verification and packaging. The five focused packaging/bridge/evidence files passed 32 tests; Flutter analysis is clean; the full suite passed 145 with one Windows-only junction skip; and the authorization preflight still reports only L1/L2 locally ready with no external action authorized.
- A post-commit deterministic prepare/verify round trip pinned exact implementation commit `5c2b24c876eec0db7bc4359aedff57259a8f46f0` and matched workflow SHA-256 `6c9be098432e188f930feb6ba8836e5e194bcfae983b1d5f0b9b0590750e7f21` byte-for-byte. Both temporary candidates were removed. The correction is published but has not been executed on Windows.
- Exact candidate head `a85c7e2f4be5fef263039d8da33c30719ba5c672` was pushed as a leased fast-forward from remote head `ddde50cf96bf5cd86bc3aa578b893a720b4f2840` to `codex/windows-packaging`. Push CI run `31445017225` passed API job `93637224171` and Flutter job `93637224110`; pull-request CI run `31445020079` passed API job `93637232496` and Flutter job `93637232536`.
- PR #25 remains open, draft, mergeable, and based on `codex/yamaha-dme5-dme3`; its exact head is now `a85c7e2f4be5fef263039d8da33c30719ba5c672`, with all four current API/Flutter checks green. It remains an accumulated integration comparison view and was not otherwise changed during bridge publication.
- Fresh local worktree `/private/tmp/showvault-windows-evidence-bridge-a85c7e2` is on branch `codex/windows-evidence-bridge-native-packages-a85c7e2` from exact `main` `43242c03bb6b8ee76eec3fc9debdd6f2c6daf850`. Live read-only refs confirmed that exact `main` and published source `a85c7e2f4be5fef263039d8da33c30719ba5c672` before preparation.
- Deterministic preparation and independent pre/post-commit verification produced exact local bridge commit `a307e5499d3396027bcc67fc4856ff236542fb31`, pinning source `a85c7e2f4be5fef263039d8da33c30719ba5c672` with workflow SHA-256 `d8458e7981b2fbc361cdb0131513456a2e67f32367c42027c72bb74b0ea5fc28`. Its only changed path is `.github/workflows/windows-evidence.yml`, with 31 insertions and one deletion; 19 focused packaging/bridge tests passed and `git diff --check` is clean.
- The bridge preserves `workflow_dispatch` only, `contents: read`, the exact three action SHAs, immutable checkout with credentials disabled, Windows 2025/90-minute bounds, the five-package native install, full proof/checksum/cleanup stages, and 14-day retention. Exact commit `a307e5499d3396027bcc67fc4856ff236542fb31` was pushed with the verified old-head lease, and draft PR #29 targets `main` at that exact head. Push run `31446160889` and pull-request run `31446174726` passed all four API/Flutter jobs. No ready transition, merge, dispatch, artifact retrieval, or equipment access occurred.
- After exact live-state, source-pin, digest, deterministic-byte, one-file-diff, and four-check revalidation, PR #29 was marked ready and merged with the expected-head guard at exact reviewed head `a307e5499d3396027bcc67fc4856ff236542fb31`.
- The resulting `main` is `b6d5aff28a310f3ccc3d7a6e1b38c2589170d5fa`; its parents are prior `main` `43242c03bb6b8ee76eec3fc9debdd6f2c6daf850` and exact bridge head `a307e5499d3396027bcc67fc4856ff236542fb31`. Main's workflow is byte-identical to the deterministic candidate, pins exact source `a85c7e2f4be5fef263039d8da33c30719ba5c672`, and retains SHA-256 `d8458e7981b2fbc361cdb0131513456a2e67f32367c42027c72bb74b0ea5fc28`.
- Automatic `main` CI run `31446640821` passed at exact merge commit `b6d5aff28a310f3ccc3d7a6e1b38c2589170d5fa`: API job `93642190631` and Flutter job `93642190622` are green.
- The Windows workflow run list remains exactly the three earlier failed runs `31437839213`, `31441006486`, and `31443483665`. No new dispatch, rerun, artifact retrieval, or equipment access occurred during the merge gate.
- The native failures had two local causes. GitHub's Windows checkout used CRLF while several policy checks assumed LF-only workflow text. Restore target validation also extracted a final component using only `Platform.pathSeparator`, so mixed Windows separators could leave the full absolute path as the target name; containment comparison likewise needed Windows separator/case canonicalization.
- Commit `2ebfe45` canonicalizes trusted workflow/policy input to LF while continuing to reject residual carriage returns and candidate substitutions. Commit `05ed93d` extracts Windows target leaf names across both separators and uses canonical Windows comparison for directory membership. These fixes are published in product source `6d6f47ac3b32041e56238060b8b2cd8e13485a2d` but have not yet been executed on Windows.
- The post-run verifier accepts a workflow run ID and absent output directory, requires a completed successful manual run with the expected identity, fetches the workflow at its exact head SHA, verifies the manual/read-only/provenance boundary and single immutable source pin, downloads only the named artifact, invokes the independent artifact verifier, and matches artifact provenance to the actual run ID, attempt, and pin. Six focused positive/adversarial tests pass. It performs no dispatch or repository mutation.
- The bridge preparer accepts an approved lowercase full source SHA, the audited product workflow, and an absent `windows-evidence.yml`. It requires the manual/read-only policy and exactly the three approved commit-pinned actions, injects one immutable checkout ref with persisted credentials disabled, refuses overwrite and linked input, rereads the output, and emits a bounded digest. Seven focused tests and a real local dry run pass; no bridge branch or GitHub state was changed.
- The independent bridge verifier reads only regular bounded source/candidate files, regenerates the expected bridge in memory, requires the exact filename and byte-for-byte equality, and emits the verified source SHA and workflow digest. Six focused tamper/substitution tests plus a real prepare-to-verify dry run pass. It performs no write or GitHub operation.
- The accumulated PR #25 history is now locally decomposed: 247 paused legacy catalog/Agent commits precede 40 product-directed desktop/local-first/storage/resilience/Windows commits. Their net patches overlap 29 files, so blind tail cherry-picking is not an accepted integration strategy. The documented recommendation keeps PR #25 as a comparison view, integrates PRs #3-#24 first, then reconstructs six reviewable local-first product milestones while keeping the legacy expansion separate.
- A read-only local preflight now verifies the six integration manifests directly from Git: 52 selected commits, 136 selected paths, all milestone counts and overlaps, exactly 29 legacy-overlap paths, and the four excluded interleaved commits. Its bounded JSON explicitly reports that no external state was read and no repository mutation occurred.
- `docs/PR_3_24_LOCAL_DEPENDENCY_LEDGER.md` records the static local dependency chain for PRs #3-#24 from existing remote-tracking refs: 22 branch heads, 32 commits, and a 237-file combined net diff. Every adjacent head is locally ancestral, but live PR state must be revalidated only after explicit authorization.
- A second read-only preflight now verifies every PR #3-#24 local head, adjacent ancestry edge, per-row commit/file/text-line/binary count, and the combined 22-ref/32-commit/237-path topology. The combined diff includes 16,079 additions, 106 deletions, and 31 binary paths; its bounded JSON reads no external state and performs no repository mutation.
- `docs/LOCAL_AUTHORIZATION_READINESS_MATRIX.md` maps bounded local work and every fetch, push, PR mutation, merge, dispatch, artifact, installed-proof, equipment, cloud, venue/personal-data, and destructive operation to its prerequisite, explicit approval, and mandatory stop point. Only requested bounded local reads/implementation are currently ready.
- A combined local integration-plan command now runs both Git preflights and returns one bounded foundation/reconstruction result while explicitly reporting that external authorization was not evaluated.
- The authorization-readiness verifier requires the exact 15-operation matrix, approval text, order, and fail-closed current decision. It reports only L1/L2 locally ready, 13 operations blocked, and no external authorization, read, or mutation.
- `docs/LOCAL_FIRST_INTEGRATION_AUDIT.md` classifies all 29 shared files as carry, split, regenerate, or compatibility work; assigns each to a milestone; and records local-only reproduction and extraction gates. The overlap comprises 7 repository/documentation files, 4 desktop files, 11 Agent compatibility files, and 7 API/persistence files. Dashboard, Agent executor, API startup, combined tests, and the EF snapshot must be reconstructed or regenerated rather than replayed wholesale.
- `docs/LOCAL_FIRST_MILESTONE_1_EXTRACTION.md` fixes the first reconstruction milestone to the nine-commit `310190c..ce5be25` source range and all 41 net files: 23 legacy overlaps plus 18 range-only files. It excludes two net-zero navigation changes and separates tenant-scoped persistence/API, exact-candidate desktop Scan, guarded beta authentication/packaging, bounded Agent compatibility, and current documentation into reviewable commits with explicit test, migration, privacy, and authorization gates.
- `docs/LOCAL_FIRST_MILESTONE_2_EXTRACTION.md` fixes the second reconstruction milestone to the six-commit `ce5be25..c172e49` source range and all 36 net files. It separates the opaque candidate-key Save contract, immutable offline Save engine, native permission/restart rehydration, bounded Agent vault compatibility, and product runbooks; requires the later `ddfcaa6` configured-storage correction; and records exact verification/privacy gates.
- `docs/LOCAL_FIRST_MILESTONE_3_EXTRACTION.md` fixes the third reconstruction milestone to the complete ten-commit `c172e49..fff4434` range and all 31 net files. It includes the `a62649f` sandbox-safe selected-target Restore and `a7eee0d` immediate status-refresh corrections from the outset, separates durable queue execution, attended offline Restore, authenticated tenant storage, and UI reconciliation, and requires the filesystem backend to remain Development/test-only until the deployable provider milestone.
- `docs/LOCAL_FIRST_MILESTONE_4_EXTRACTION.md` fixes the fourth reconstruction milestone to the two-commit `fff4434..69b83ab` range and all 28 net files. It separates provider abstraction/validation, immutable S3 object storage, readiness/smoke behavior, pinned non-root image plus migration topology, and operations/rollback; explicitly denies production filesystem fallback, runtime delete permission, embedded credentials, and unapproved real-cloud cleanup.
- `docs/LOCAL_FIRST_MILESTONE_5_EXTRACTION.md` fixes the fifth reconstruction milestone to the seven-commit `69b83ab..3a5e715` range and all 19 net files. It separates compile-time-gated resilience commands, installed synthetic failure/restart automation, explicit bounded support diagnostics, and forward application-replacement/source-free-rehydration proof while preserving external-vault retention and strict ownership cleanup.
- `docs/LOCAL_FIRST_MILESTONE_6_EXTRACTION.md` fixes the final milestone to 18 selected Windows commits and their 35-path union. It excludes four interleaved local-first planning commits, separates current-user packaging/path policy, marker-scoped installed proof, manual provenance workflow, artifact/run attestation, and deterministic bridge prepare/verify tooling, and keeps native/attended execution separately authorized. It also correctly assigns `ddfcaa6` to conditional `LocalVaultLayout` construction in `RecoveryPackageWriter`.
- `docs/LOCAL_FIRST_INTEGRATION_CONSISTENCY_AUDIT.md` proves milestones 1-5 form an uninterrupted 34-commit chain and milestone 6 contributes 18 selected commits while excluding exactly four unrelated interleaved planning commits. The complete selection is 52 commits and a 136-path union with the same 29 legacy-overlap files; all manifest boundaries, dependencies, and safety gates are consistent.
- `docs/LOCAL_FIRST_INTEGRATION_EXECUTION_CHECKLIST.md` consolidates the dependency-ordered six-milestone product path and the separately authorized Windows evidence/equipment paths. It defines authorization classes, base/branch rules, focused/full/security loops, special milestone gates, stop conditions, and a bounded completion record. It forbids stale-base concurrent implementation and prevents native evidence from approving product integration.
- **Restore** is available from a locally verified recovery point while signed out. It reverifies independent and package manifests plus the exact content tree, accepts only an absent or operator-selected empty regular target, rejects links/substitutions/unsafe paths, copies through owned staging, verifies staged and published bytes, and writes path-free local evidence. A picker-selected existing target keeps staging inside the sandbox-authorized directory and publishes a fixed `ShowVault Restored Files` child; absent programmatic targets retain direct publication.
- Cancellation, timeout, source mutation, target mutation, and interrupted-restart failures publish no partial completion. Cleanup removes only staging with a matching bounded ownership marker and never alters the immutable recovery point.
- Normal builds do not expose the direct folder substitute. **Synchronize pending** is available when a signed-in hosted transport has organization/venue context; an isolated direct-substitute build requires both explicit synthetic fixture-home and synthetic object-store defines.
- The API returns the already stored opaque candidate key to the authorized desktop; tests prove the key and evidence remain path-free.
- `docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md` records the recommended commercial structure: desktop app, customer web portal, and private ShowVault Admin web console; branded ShowVault authentication backed by Auth0; Stripe one-time license plus recurring tiers; and no staff access to customer passwords.

## Installed personal-Mac evidence

- Final app artifact: `/tmp/showvault-macos-personal-beta-no-login-20260810/ShowVault.app`
- Transfer ZIP: `/tmp/showvault-macos-personal-beta-no-login-20260810/ShowVault-macos.zip`
- ZIP SHA-256: `ed67ec092c9452d96250d9cff710330330547d5e3314c0deab407888147e61ad`
- App version/build: `0.1.0 (1)`
- Bundle ID: `com.showvault.app`
- Architecture: universal `x86_64` + `arm64`
- Signing: ad hoc, not notarized; personal attended testing only
- Test Mac: macOS 26.3 build 25D125
- Control plane: explicitly guarded local Development API at `http://127.0.0.1:5000` backed by local PostgreSQL
- Final verified no-login scan ID: `4f016f10-9f0b-452b-8c52-94084efc9217`
- Scan result: completed with three candidates—Resolume Arena installed application, Serato DJ Pro installed application, and Serato DJ Pro user-data root
- Database privacy check: candidate count 3; no `/` separator in any stored candidate key or evidence field
- No login, Keychain, filesystem permission, or security prompt appeared during the final direct scan.

Do not copy exact local source paths into control-plane evidence or future documentation. The test above intentionally records only path-free product/type results.

## Installed synthetic permission/restart evidence

- A release-mode macOS build used the compile-time `SHOWVAULT_SYNTHETIC_FIXTURE_HOME` isolation seam. Synthetic mode redirects catalog user-data roots and suppresses real installed-application candidates; normal builds omit the define.
- The native picker selected the exact synthetic Serato-shaped source and a separate synthetic vault. Save produced two copied fixture files, matching package/independent manifests, and one queued JSON record.
- The independent manifest filename matched its SHA-256 digest.
- After terminating and relaunching the app, **Open local vault** restored `1 verified • 1 cloud queued` without running Scan.
- The final normal release build was rebuilt without the synthetic define. The current build is a 50.0 MB universal app, and its signed entitlements include `com.apple.security.files.user-selected.read-write`.
- No real `Documents/ShowVault Pro` vault was created and no personal source permission was granted. A preliminary shell-`HOME` isolation attempt was canceled when macOS retained the real home context; no Save occurred. This is why installed drills must use the compile-time synthetic fixture seam rather than shell environment redirection.

## Installed authenticated sync and attended restore evidence

- Drill root and release artifact: `/private/tmp/showvault-hosted-drill-20260810` and its isolated `ShowVault.app`. All source, vault, hosted-storage, and restore locations were newly created synthetic directories; no personal application data or Keychain access was used.
- The release build used `SHOWVAULT_SYNTHETIC_FIXTURE_HOME`, loopback API configuration, guarded Development personal-beta authentication, and test-only 8-byte/2-second chunks. It intentionally omitted `SHOWVAULT_SYNTHETIC_OBJECT_STORE_ROOT`, so bytes traversed the authenticated hosted API.
- The first immutable recovery point ID is `02387f1ea7b6610d6e47d790d7b6003a0fb6ad2b70d97919f8328d768ffa3e88`. The source and local package contained two files totaling 185 bytes.
- Synchronization was terminated after durable partial hosted bytes existed. Relaunching the installed app and reopening the exact vault rehydrated the queue, resumed from server length, and produced `syncing` attempt 1, `syncing` attempt 2, then one `synchronized` completion.
- The committed package is under GUID-derived organization/venue segments and contains exactly content, `manifest.json`, and `receipt.json`. Every local/remote content SHA-256 matched. The hosted manifest contains no `/private/`, `/tmp/`, fixture, vault, or hosted-storage path. A request without authentication returned `401`.
- Installed Restore through the native picker initially exposed a real macOS sandbox defect: sibling staging was outside the selected target grant and failed with `Operation not permitted`. The correction stages inside the selected target and atomically publishes `ShowVault Restored Files`. The rebuilt installed app then restored the exact two files and 185 bytes, both SHA-256 values matched, no staging remained, and the evidence record was path-free.
- A second isolated local vault created recovery point `13ec0cabebb5329277fd1d56d9d38faad035bed8baf9d7abbc3e0e39dff5d544`. With the API stopped, the installed app preserved it locally verified and recorded `retry` attempt 1 with a 30-second next-attempt time. After the API returned, the same durable job completed as `synchronized` attempt 2 with exact hosted checksums.
- That final run also exposed a stale per-recovery chip: the vault summary refreshed while the chip retained its initial queued result. The dashboard now prefers the rehydrated vault record, so synchronization status refreshes immediately without restart.
- A preliminary synthetic picker selection accidentally targeted the fixture's `Music` parent. The app was stopped before proceeding; ShowVault-owned artifacts were moved intact to `misselected-vault-attempt`, the fixture was restored cleanly, and the quarantined attempt was excluded from evidence. No personal path or data was involved.
- Automated transport/API coverage still proves bearer authorization, tenant binding, missing-session/viewer/outsider denial, resume/idempotency, tamper rejection, path safety, concurrent completion, and linked-storage rejection. Restore tests cover absent and selected-empty targets, internal interrupted staging, non-empty/linked targets, tamper/mutation, cancellation, containment, evidence, and signed-out UI wiring.
- The installed app evidence above predates the S3 adapter and remains valid protocol evidence. The adapter has only disposable-emulator execution evidence. Do not claim selected-provider retention, regional durability, billing enforcement, bandwidth scheduling, personal-data readiness, Windows runtime readiness, notarization, venue readiness, or Recovery Confidence.

## Deployable object-storage evidence

- The production provider uses AWS SDK S3 conditional create, bounded reads, paginated listings, and the standard credential chain. No credential fields were added to application options.
- Configuration validation rejects disabled or filesystem storage outside Development, invalid bucket/prefix values, and non-HTTPS custom endpoints outside Development.
- Object contract tests cover tenant isolation, resume, duplicate/conflicting chunks, incomplete commit, tamper, unexpected objects, concurrent/idempotent completion, unavailable storage, path-free hashed keys, and production fail-closed behavior.
- The image built cleanly from exact .NET SDK/runtime tags. PostgreSQL `18.3-alpine3.23` became healthy, the one-shot migration exited 0, and the API reported healthy liveness and S3-backed readiness.
- Pinned disposable MinIO created the private test bucket. The final committed-source image smoke command wrote a random synthetic package, repeated its first chunk, resumed from the persisted offset, verified the exact checksum, and committed twice with one stable receipt. Evidence package ID: `1c258353eef3f14ae274f0300a12162968030751adb0f74a5d5483e95d045199`.
- MinIO is emulator evidence only. No production cloud bucket, IAM policy, TLS ingress, retention rule, replication, monitoring, backup, or regional failure test was configured.
- Docker Desktop intermittently left attach/start client calls waiting while containers remained `Created`; stopping only those exact disposable client processes and issuing a fresh start completed the proof. This was local tooling behavior, not a ShowVault service failure.

## Installed automated resilience evidence

- Final artifact directory: `/private/tmp/showvault-resilience-matrix-final-20260810`
- App version/build: `0.1.0 (1)`; bundle ID `com.showvault.app`; universal `x86_64` + `arm64`; ad hoc signed and strictly validated; sandbox, network-client, and user-selected read/write entitlements present.
- Installed executable SHA-256: `c6f3f59d4b94c525d2798955887a31124e681d2cf83e72a1256b9f3a7c8a03cb`
- ZIP SHA-256: `cd3a51917bfd4471d7c86738cf4ea1a130510279ed5038bdc8699cbc43fb6c7f`
- Report file SHA-256: `6f7a4fb018891faee17c396cde51ce0970479fe62de50e14361e535662a7d122`
- Report core evidence SHA-256: `18bc1c6cc2b3b15ccac4e773d35df71d61ec72da5cf9e7061e7f7756cf7bee73`
- Seven report phases passed. Two packages contained four verified files/128 bytes. API loss preserved the first package at retry attempt 1. A deliberate first-chunk cancellation left exactly 8 durable remote bytes at attempt 2 with no receipt; a fresh installed-app process resumed and synchronized on attempt 3, accepted duplicate completion idempotently, and restored two files/64 bytes.
- MinIO loss made readiness unavailable, preserved the second local package at retry attempt 1, and published no receipt. After MinIO restarted, the same queue synchronized on attempt 2.
- Seven negative cases passed: source mutation published no package; local tamper, corrupt remote bytes, incomplete remote bytes, and a conflicting duplicate chunk published no receipt; the non-empty target was unchanged; interrupted Restore published nothing.
- The report contains no source/vault/restore path, credential, token, content, or host identity. ZIP/report and report-core hashes verified. Owned sandbox state plus disposable containers, networks, and volumes were removed.
- Three preliminary failed generated artifacts were moved recoverably to Trash while the executable-name and sandbox-root assumptions were corrected. They contain only synthetic loopback test builds; they were excluded from evidence.
- This is process-restart evidence, not host-reboot evidence. MinIO is not a production-provider claim. Native picker behavior remains supported by the earlier attended drill, not this command-mode matrix.

## Installed upgrade and support-diagnostic evidence

- Final artifact directory: `/private/tmp/showvault-upgrade-diagnostic-final-20260810`
- Before ZIP SHA-256: `47b22ad7d022f405e856ddd55e3c4d5c2d12139a5450d88dd2813d00b7020971`
- After ZIP SHA-256: `2f9ae2078bb31235505dd89931932aa21fc6f9cb465138cb73baae82ec3a1788`
- Report file SHA-256: `780f50a6dd924f7349fe34dd65114e26b420ca048a4dcc50a5be10a00a665db6`
- Report core evidence SHA-256: `e9a8d46207dd299d6f79ec9dc761422af02f5ad9b2f3bee59b57359f4ca1eb51`
- The before/after executable SHA-256 values were distinct. Both copied release apps passed strict deep code-signature validation with ad hoc personal-test signing.
- The before app created one two-file immutable recovery point, recorded an unavailable retry, synchronized it at attempt 2, accumulated four append-only state events, performed one exact Restore, generated a diagnostic, and deleted the synthetic source.
- The after app replaced the fixed installed path and, without the source, verified the unchanged package and independent manifest, synchronized journal, restore evidence, and source-free rehydration. It generated a second checksummed diagnostic.
- The exported report contains no local path, source content, credential, token, raw error, or host identity. Report-core and outer artifact checksums verified; the owned synthetic sandbox was removed.
- One earlier proof generated before queue-history validation was tightened was moved recoverably to Trash and excluded. Generated iOS SwiftPM resolution side effects from the macOS builds were also moved recoverably to Trash and were not committed.
- This is controlled forward macOS application-replacement evidence. It is not clean-machine reinstall, rollback, host-reboot, Windows, distribution-signing/notarization, production-provider, personal-data, or venue evidence.

## Verification baseline

- Flutter analysis: no issues
- Flutter tests: 145 passed; 1 Windows-only NTFS-junction test skipped on macOS
- Local-first integration preflight: 4 focused tests passed; real local Git topology passed with 52 commits, 136 paths, 29 legacy overlaps, and no external read or mutation
- PR #3-#24 dependency preflight: 4 focused tests passed; real local Git topology passed with 22 refs, 32 commits, 237 paths, 16,079 additions, 106 deletions, 31 binary paths, and no external read or mutation
- Combined local integration-plan preflight: 4 focused tests and real local execution passed; authorization is explicitly not evaluated
- Authorization-readiness preflight: 4 focused drift/weakened-boundary tests and real local execution passed; 2 operations locally ready and 13 blocked
- Contracts tests: 2 passed
- Agent tests: 429 passed
- Platform tests: 28 passed
- API tests: 29 passed, including hosted-sync authorization plus filesystem and object-store tenant isolation, integrity, privacy, resume, duplicate, tamper, concurrency, unavailability, and idempotency coverage
- Focused hosted-sync tests: 14 passed
- Disposable S3-compatible smoke: passed with immutable write/resume/commit/idempotency behavior
- Deployable Compose configuration validation: passed
- API container image build: passed
- Installed macOS resilience matrix: 7 report phases and 7 safe negative cases passed
- Installed macOS upgrade proof: two distinct artifacts; replacement, source-free rehydration, manifest, attempt-2/four-event queue journal, restore evidence, path-free diagnostics, checksums, and cleanup passed
- Windows packaging: implementation and host-independent static/path-policy coverage passed; the authorized Windows runner reached analysis/tests but test failures stopped native package compilation and installed execution
- Windows-native CI bridge: PR #26 merged exact bridge commit `5586b25408df9fb36889b71b565fb5517de2d69c` into `main` at `b211804c1d01f93e0d64b7e766637892fd0bb7fe`, pinning green product source `f3b7a1deeef76004fbc7c65f6256e6b24043916d`
- Windows-native run `31437839213`: toolchain and analysis passed; tests stopped the job at 117 passed/15 failed/10 skipped; no package, installed proof, provenance, checksum, upload, or artifact exists
- Local Windows failure corrections: 23 focused line-ending/policy tests passed, 25 focused restore/path-policy tests passed, the full Flutter suite passed 144 with 1 macOS-skipped junction test, analysis is clean, and the real bridge prepare/verify round trip reproduced digest `e2bff6150fc129c78f16663bb2281a7c48fe3154e4008a55712ecf1575320131`
- Windows-native run `31441006486`: exact-source checkout, pinned Flutter, toolchain verification, analysis, and 135 Flutter tests passed with 10 skipped; normal packaging failed because the app did not enable vcpkg before CMake `project()`, leaving `cpprestsdk` unresolved; no installed proof, provenance, checksums, cleanup phase, upload, or artifact exists
- Windows-native run `31443483665`: exact-source checkout, pinned Flutter, vcpkg-aware toolchain verification, analysis, and 136 Flutter tests passed with 10 skipped; normal packaging entered the vcpkg toolchain but failed because `cpprestsdk` was not installed in the runner's vcpkg tree; installed proof, provenance, checksums, cleanup, and upload were skipped; GitHub reports zero artifacts and the run was not rerun
- Local Windows native-package correction: exact five-package `x64-windows` install/result verification is ordered before analysis and packaging; 32 focused packaging/bridge/evidence tests passed, analysis is clean, the full suite passed 145 with 1 macOS-skipped junction test, authorization remains L1/L2 only, and the exact `5c2b24c` bridge round trip reproduced digest `6c9be098432e188f930feb6ba8836e5e194bcfae983b1d5f0b9b0590750e7f21`
- Product CI at published native-package head `a85c7e2f4be5fef263039d8da33c30719ba5c672`: push run `31445017225` and pull-request run `31445020079` both passed; all four API/Flutter jobs are green
- Local native-package bridge: exact commit `a307e5499d3396027bcc67fc4856ff236542fb31` is a one-file 31-insertion/1-deletion change from `main` `43242c0`; preparation plus independent pre/post-commit verification match digest `d8458e7981b2fbc361cdb0131513456a2e67f32367c42027c72bb74b0ea5fc28`; 19 focused tests and diff check pass
- Native-package bridge merge: PR #29 merged exact reviewed head `a307e5499d3396027bcc67fc4856ff236542fb31` at `b6d5aff28a310f3ccc3d7a6e1b38c2589170d5fa`; merge parents, byte identity, exact source pin, and digest were independently revalidated
- Merge CI at `b6d5aff28a310f3ccc3d7a6e1b38c2589170d5fa`: automatic push run `31446640821` passed API job `93642190631` and Flutter job `93642190622`
- Product CI at published source `f3b7a1deeef76004fbc7c65f6256e6b24043916d`: all four checks passed
- Final harness ZIP/report checksums and internal evidence checksum: passed
- Final harness strict deep code-signature validation: passed
- Normal macOS release clean rebuild and strict deep code-signature validation: passed
- EF Core migrations `20260810003349_AddDesktopCatalogScanCandidates` and `20260810003907_AddDesktopCatalogScans` are applied to the local database
- EF Core pending-model check: no pending changes
- macOS release build: 50.2 MB build report (48 MiB on disk), universal `x86_64` + `arm64` app; ad hoc signed and strictly validated with sandbox user-selected read/write access
- ZIP checksum matches `SHA256SUMS`
- `git diff --check`: passes
- Local integration decomposition: eight bounded commit ranges account for all 287 post-PR #24 commits; the final 40-commit product patch changes 123 files and overlaps the paused legacy patch in 29 files
- Local overlap audit reproduction: 247 legacy commits, 40 product-directed commits, and exactly 29 shared paths; file-level milestone dispositions recorded
- Milestone 1 manifest reproduction: 9 historical commits, 41 net files, 23 legacy-overlap files, 18 range-only files, and 2 transient net-zero navigation exclusions
- Milestone 2 manifest reproduction: 6 historical commits, 36 net files, 14 legacy overlaps, 22 range-only files, 14 milestone-1 overlaps, 8 introduced files, and no transient net-zero files
- Milestone 3 manifest reproduction: 10 historical commits, 31 net files, 4 legacy overlaps, 7 milestone-1 overlaps, 10 milestone-2 overlaps, 18 introduced files, and no transient net-zero files
- Milestone 4 manifest reproduction: 2 historical commits, 28 net files, 17 introduced files, 3 legacy overlaps, 3 milestone-1 overlaps, 2 milestone-2 overlaps, 9 milestone-3 overlaps, and no transient net-zero files
- Milestone 5 manifest reproduction: 7 historical commits, 19 net files, 9 introduced files, 3 legacy overlaps, 7 overlaps with each of milestones 1-3, 3 milestone-4 overlaps, and no transient net-zero files
- Milestone 6 manifest reproduction: 18 selected commits, 35-path union, 21 paths absent at the milestone-5 base, and verified overlap counts of 2 legacy/5 milestone-1/7 milestone-2/6 milestone-3/2 milestone-4/6 milestone-5 paths
- Cross-manifest consistency: milestones 1-5 cover 34 uninterrupted commits/112 net files; milestone 6 selects 18 commits/35 paths; combined selection is 52 commits/136 paths with 11 cross-track shared paths and 29 legacy overlaps

## Safety boundaries

- Never request or approve access to the user's personal login Keychain for ShowVault.
- Never enable the no-login bypass for staging, production, a non-loopback endpoint, or a distributed customer build.
- ShowVault administrators may view account and entitlement status but must never see or retrieve customer passwords.
- Never store Auth0 credentials, tokens, enrollment codes, or client secrets in the vault. Exact source paths may appear only in protected local recovery metadata where required for restore and must not leak into cloud-facing logs or path-free discovery evidence.
- Never enumerate unrelated installed applications, arbitrary directories, disks, networks, or machine identity.
- Detection is not backup, verification, restore, or recovery readiness. Preserve those states separately.
- Do not weaken Gatekeeper or system-wide security for the ad hoc personal-test build.
- Do not claim notarization, clean-machine installation, Windows runtime readiness, venue readiness, or complete recovery readiness without direct evidence.
- Do not use venue equipment, networks, credentials, paths, topology, or data without new explicit authorization.

## Exact next bounded objective

After explicit authorization for exactly one manual Windows workflow dispatch, revalidate `main` `b6d5aff28a310f3ccc3d7a6e1b38c2589170d5fa`, its exact merge parents, byte-identical workflow, source pin `a85c7e2f4be5fef263039d8da33c30719ba5c672`, digest `d8458e7981b2fbc361cdb0131513456a2e67f32367c42027c72bb74b0ea5fc28`, and unchanged three-run history. Dispatch exactly one run and wait for completion. On failure, do not rerun; on success, stop before artifact retrieval or verification.

The next slice must satisfy these boundaries:

1. Three authorized Windows evidence runs now exist and all failed at distinct bounded gates. None is authorized for rerun. The native-package correction's exact one-file bridge is merged on `main` at `b6d5aff`. A new exactly-one workflow dispatch is next and remains separately authorized; artifact retrieval remains a later gate only after a successful run.
2. Treat PR #25 as an accumulated integration draft, not a Windows-only change. Follow `docs/WINDOWS_EVIDENCE_INTEGRATION_PLAN.md` and `docs/LOCAL_FIRST_INTEGRATION_AUDIT.md`: keep PR #25 as a comparison view, integrate PRs #3-#24 first, and reconstruct the six local-first milestones using the file-level dispositions for all 29 overlaps. Do not merge the accumulated draft merely to expose the workflow.
3. Generate the refreshed one-file bridge with `dart run tool/prepare_windows_evidence_bridge.dart <approved-source-sha> ../../.github/workflows/windows-evidence.yml <bridge-worktree>/.github/workflows/windows-evidence.yml`, then run `dart run tool/verify_windows_evidence_bridge.dart` with the same three arguments, confirm matching digests and the one-file diff, and only then follow the separately authorized review/merge sequence. After an approved manual run completes, use `dart run tool/verify_windows_run.dart <workflow-run-id> <absent-output-directory>` and review its bounded output. Treat it as native headless evidence, not attended picker/Auth0 or clean-customer-machine evidence.
4. For a controlled computer, use PowerShell 7, Flutter Windows tooling, Visual Studio Desktop C++, and Inno Setup 6 on the build side. The installed target must not require Flutter, Git, the repository, or a separate Agent.
5. Review `docs/WINDOWS_PACKAGING_AND_EXECUTION.md`, then run the normal package command and `tool/run-windows-installed-proof.ps1` into absent local-drive output directories.
6. Confirm native PowerShell and Inno parsing/build, complete deployment, current-user `showvault://` registration, installer launch/uninstall, package manifest, checksums, and actual Authenticode states.
7. Run the Windows-only NTFS-junction test and the complete Flutter suite on Windows. Confirm drive/UNC/device/traversal/ADS/case/containment rules and selected-folder behavior.
8. On controlled attended equipment, execute installed exact catalog Scan, offline Save/Verify, process restart, durable queue rehydration, attended Restore, explicit diagnostics, and before/after replacement with the source removed.
9. Verify retention and cleanup, record exact evidence/limitations, and keep reboot, commercial-session expiry, provider quota/outage, distribution signing, personal data, clean-machine support range, and venue use separate.

## Required workflow

1. Read this file and inspect Git status and recent commits.
2. Preserve unrelated changes and keep `NEXT_CONVERSATION.md` untracked.
3. State one bounded outcome before material changes.
4. Inspect only task-relevant code, tests, contracts, migrations, ADRs, and documentation.
5. Continue on the current branch for this handoff; create a new `codex/` branch for a genuinely new slice.
6. Prefer the smallest safe venue-neutral vertical slice.
7. Use synthetic fixtures by default and personal equipment only when explicitly authorized.
8. Run focused checks, then the relevant full regression, build, migration-model, checksum, and diff checks.
9. Audit privacy, tenant isolation, authorization, path containment, accidental local persistence, and content/network behavior.
10. Commit implementation and handoff documentation separately.
11. Refresh this file after completing the bounded task and keep `NEXT_CONVERSATION.md` copy/paste-ready but untracked.

## Preferences

- Always call the intended first venue LIV nightclub, never Live Nightclub.
- Keep the customer experience plain and obvious; avoid exposing infrastructure concepts.
- Keep macOS and Windows as product requirements even when current runtime evidence is macOS-only.
- Act autonomously on safe, in-scope work and make evidence-backed assumptions.
- Use official primary sources for protocol/product claims.
- Communicate concise outcome-first progress updates at meaningful milestones.
- Exact conversation-context usage is unavailable; do not invent percentages.
- When the Product Owner says **start next task**, complete the next two safe bounded local tasks in the same turn when dependencies permit, keep their implementation commits distinct, and always state the next step at the end of the final response.

## Reference map

- `docs/PROTOTYPE_READINESS.md` — venue-neutral readiness gates and direct desktop boundary
- `docs/LOCAL_QUEUE_SYNC.md` — desktop queue journal, substitute transport, resumability, verification, and privacy boundary
- `docs/DEPLOYABLE_PROTOTYPE_STORAGE.md` — container deployment, S3 key/credential boundary, smoke check, cleanup, migration, and rollback
- `docs/INSTALLED_RESILIENCE_MATRIX.md` — installed release command-mode gates, scenario matrix, path-free evidence, and limitations
- `docs/UPGRADE_AND_SUPPORT_DIAGNOSTICS.md` — diagnostic schema/boundary, upgrade/removal semantics, installed replacement evidence, and limitations
- `docs/WINDOWS_PACKAGING_AND_EXECUTION.md` — Windows package boundary, local-path rules, installed-proof procedure, current blocker, and claim limits
- `docs/WINDOWS_EVIDENCE_INTEGRATION_PLAN.md` — one-file default-branch evidence bridge and separate accumulated product-integration path
- `docs/LOCAL_FIRST_INTEGRATION_AUDIT.md` — exact 29-file overlap, milestone dispositions, reproduction commands, and extraction gates
- `docs/LOCAL_FIRST_MILESTONE_1_EXTRACTION.md` — complete direct-Scan milestone source/file manifest, reconstruction order, exclusions, and verification gates
- `docs/LOCAL_FIRST_MILESTONE_2_EXTRACTION.md` — complete local-vault/offline-Save milestone manifest, native-access boundary, compatibility correction, and verification gates
- `docs/LOCAL_FIRST_MILESTONE_3_EXTRACTION.md` — complete durable-sync/authenticated-hosting/Restore milestone manifest and installed-drill corrections
- `docs/LOCAL_FIRST_MILESTONE_4_EXTRACTION.md` — complete deployable object-storage milestone manifest, credential/immutability boundaries, topology, smoke, migration, and rollback gates
- `docs/LOCAL_FIRST_MILESTONE_5_EXTRACTION.md` — complete installed-resilience/support-diagnostic/upgrade-preservation milestone manifest and synthetic execution gates
- `docs/LOCAL_FIRST_MILESTONE_6_EXTRACTION.md` — complete Windows packaging/native-evidence milestone manifest, selected-commit accounting, provenance/attestation boundaries, and authorized native gates
- `docs/LOCAL_FIRST_INTEGRATION_CONSISTENCY_AUDIT.md` — six-manifest commit/file coverage proof, exclusions, legacy separation, dependency and safety consistency
- `docs/LOCAL_FIRST_INTEGRATION_EXECUTION_CHECKLIST.md` — controlling dependency order, authorization ledger, common verification loop, Windows evidence/equipment tracks, stop conditions, and completion record
- `docs/PR_3_24_LOCAL_DEPENDENCY_LEDGER.md` — local-only PR #3-#24 branch/head ancestry, per-row diff scope, review waves, and authorized live revalidation procedure
- `apps/showvault_app/tool/verify_local_first_integration_preflight.dart` — bounded read-only local Git verifier for the six integration manifests
- `apps/showvault_app/tool/verify_pr_dependency_ledger.dart` — bounded read-only local Git verifier for every PR #3-#24 ledger row and combined totals
- `docs/LOCAL_AUTHORIZATION_READINESS_MATRIX.md` — exact prerequisites, authorization classes, dependencies, and mandatory stops for local, remote, equipment, cloud, venue, and destructive operations
- `apps/showvault_app/tool/verify_local_integration_plan.dart` — combined bounded foundation/reconstruction Git preflight that does not evaluate authorization
- `apps/showvault_app/tool/verify_local_authorization_readiness.dart` — fail-closed verifier for the exact operation/approval matrix and current L1/L2-only readiness decision
- `docs/LOCAL_ATTENDED_RESTORE.md` — offline attended restore, staging, verification, cleanup, evidence, and limitations
- `docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md` — customer identity, licensing, subscription, portal, and Admin-console structure
- `docs/SYSTEM_INVENTORY_PLUGIN.md` — direct app scan versus legacy Agent compatibility
- `docs/AUTOMATIC_DISCOVERY.md` — discovery and identification safety decisions
- `docs/INTEGRATION_CATALOG.md` — authoritative catalog/testing matrix
- `docs/adr/` — architecture decisions
- `services/contracts/`, `services/api/`, `services/platform/` — control-plane authority
- `apps/showvault_app/` — customer desktop implementation
