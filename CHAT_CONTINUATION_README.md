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
- Sandbox-safe selected-target restore commit: `a62649f fix: restore safely into selected sandbox folders`
- Immediate cloud-status refresh commit: `a7eee0d fix: refresh synchronized recovery status`
- Direct-scan commit: `3ed4bdc feat: scan computers directly without agent enrollment`
- Navigation/no-login beta commit: `eea1d45 feat: restore navigation and add guarded no-login beta`
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
- The current host has no Windows Flutter target/VM, PowerShell runtime, Wine, MSVC/Windows SDK, or Inno Setup. Windows scripts and installer syntax have not run in their native engines, the NTFS-junction test is skipped, and no Windows artifact or runtime evidence exists. Windows readiness remains unclaimed.
- A manual-only `windows-2025` workflow now runs the complete native test/package/installed-proof/checksum/cleanup sequence with read-only repository permission, pinned action revisions, Flutter 3.44.8 x64, no secrets, no automatic trigger, and 14-day synthetic artifact retention. It is published on `codex/windows-packaging` in mergeable draft PR [#25](https://github.com/ChrisRivera23/ShowVault/pull/25), but it is not on the default branch and has not been dispatched.
- PR #25 targets the nearest published ancestor, `codex/yamaha-dme5-dme3`, because the intervening branch stack exists only locally. GitHub reports 287 accumulated commits and 293 changed files, so it is an integration draft rather than a Windows-only review.
- The first PR CI run exposed a Linux-runner defect: legacy Agent package-directory mode unnecessarily resolved the unavailable Documents folder. Commit `ddfcaa6` makes local-vault construction conditional while retaining the fail-closed default-vault behavior. All 429 Agent tests pass locally, and all four push/pull-request API and Flutter checks on the corrected head pass.
- The cross-platform downloaded-evidence verifier independently requires the exact two artifact directories, refuses links and unlisted files, accepts real PowerShell CRLF or LF checksum files, verifies both exact checksum sets, enforces closed package/metadata/report/provenance schemas, verifies the report-core digest, rejects path/sensitive-value leakage, and emits only bounded hashes, statuses, workflow identity, results, and limitations. Seven focused positive/adversarial tests pass. Recorded Authenticode statuses are validated as bounded evidence; signer trust remains a separate Windows check.
- The workflow adds a checksummed, path-free provenance file containing the actual checked-out commit, manual event, run ID/attempt, job, runner OS/architecture, and artifact name. Draft PR #26 currently pins the older `ddfcaa6` workflow and must not be merged or dispatched unchanged; it needs a refreshed pin after the protected source commit is explicitly published and green.
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
- Flutter tests: 106 passed; 1 Windows-only NTFS-junction test skipped on macOS
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
- Windows packaging: implementation and host-independent static/path-policy coverage passed; native package compilation and installed execution not run because no Windows environment is available
- Windows-native CI bridge: YAML parsing and static policy test passed; workflow is published in draft PR #25 but is not on `main` and has not executed
- Draft PR #25 CI: all four push/pull-request API and Flutter checks passed at `ddfcaa6`
- Final harness ZIP/report checksums and internal evidence checksum: passed
- Final harness strict deep code-signature validation: passed
- Normal macOS release clean rebuild and strict deep code-signature validation: passed
- EF Core migrations `20260810003349_AddDesktopCatalogScanCandidates` and `20260810003907_AddDesktopCatalogScans` are applied to the local database
- EF Core pending-model check: no pending changes
- macOS release build: 50.2 MB build report (48 MiB on disk), universal `x86_64` + `arm64` app; ad hoc signed and strictly validated with sandbox user-selected read/write access
- ZIP checksum matches `SHA256SUMS`
- `git diff --check`: passes

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

Publish and validate the provenance-protected source, refresh isolated draft PR #26 to that exact source, then obtain separate merge and dispatch approvals, or use explicitly authorized controlled Windows equipment.

The next slice must satisfy these boundaries:

1. The original branch push and draft PR are complete, but PR #26 predates provenance and is not merge-ready. Obtain explicit authorization before pushing the six local `codex/windows-packaging` commits or updating PR #26. Obtain separate authorization for marking ready/merging, manual dispatch, or use of a controlled Windows 10/11 x64 computer.
2. Treat PR #25 as an accumulated integration draft, not a Windows-only change. Review the 287-commit stack and choose a deliberate default-branch integration path; do not merge the accumulated draft merely to expose the workflow.
3. For the manual workflow path, ensure it exists on the default branch, dispatch it once, wait for completion, download and extract `showvault-controlled-windows-evidence`, then run `dart run tool/verify_windows_evidence.dart <artifact-directory>` from `apps/showvault_app` and review its bounded output. Treat it as native headless evidence, not attended picker/Auth0 or clean-customer-machine evidence.
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

## Reference map

- `docs/PROTOTYPE_READINESS.md` — venue-neutral readiness gates and direct desktop boundary
- `docs/LOCAL_QUEUE_SYNC.md` — desktop queue journal, substitute transport, resumability, verification, and privacy boundary
- `docs/DEPLOYABLE_PROTOTYPE_STORAGE.md` — container deployment, S3 key/credential boundary, smoke check, cleanup, migration, and rollback
- `docs/INSTALLED_RESILIENCE_MATRIX.md` — installed release command-mode gates, scenario matrix, path-free evidence, and limitations
- `docs/UPGRADE_AND_SUPPORT_DIAGNOSTICS.md` — diagnostic schema/boundary, upgrade/removal semantics, installed replacement evidence, and limitations
- `docs/WINDOWS_PACKAGING_AND_EXECUTION.md` — Windows package boundary, local-path rules, installed-proof procedure, current blocker, and claim limits
- `docs/WINDOWS_EVIDENCE_INTEGRATION_PLAN.md` — one-file default-branch evidence bridge and separate accumulated product-integration path
- `docs/LOCAL_ATTENDED_RESTORE.md` — offline attended restore, staging, verification, cleanup, evidence, and limitations
- `docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md` — customer identity, licensing, subscription, portal, and Admin-console structure
- `docs/SYSTEM_INVENTORY_PLUGIN.md` — direct app scan versus legacy Agent compatibility
- `docs/AUTOMATIC_DISCOVERY.md` — discovery and identification safety decisions
- `docs/INTEGRATION_CATALOG.md` — authoritative catalog/testing matrix
- `docs/adr/` — architecture decisions
- `services/contracts/`, `services/api/`, `services/platform/` — control-plane authority
- `apps/showvault_app/` — customer desktop implementation
