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
- Branch: `codex/installed-hosted-sync-drill`
- Local-first vault foundation commit: `bc53f4b feat: establish local-first vault foundation`
- Offline desktop Save commit: `85b3e92 feat: save desktop recovery points offline`
- Desktop permission/rehydration commit: `07e6e62 feat: authorize and rehydrate local vaults`
- Durable queue synchronization commit: `f016ad1 feat: synchronize durable local queue`
- Attended local restore commit: `36fcda9 feat: restore verified local recovery points`
- Authenticated hosted synchronization commit: `5f05f44 feat: synchronize through authenticated hosted storage`
- Installed-drill starting handoff commit: `e980165 docs: hand off installed hosted sync drill`
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
- `HostedSync:RootPath` is empty by default. The first backend is explicitly configured server-owned filesystem storage behind the API; it proves the hosted authorization/transport boundary but not production object-storage durability or distributed locking.
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
- The server backend remains controlled development filesystem storage, not a production object store. Do not claim retention policy, regional durability, distributed multi-server concurrency, billing enforcement, bandwidth scheduling, personal-data readiness, Windows runtime readiness, notarization, venue readiness, or Recovery Confidence.

## Verification baseline

- Flutter analysis: no issues
- Flutter tests: 77 passed
- Contracts tests: 2 passed
- Agent tests: 429 passed
- Platform tests: 28 passed
- API tests: 22 passed, including hosted-sync authorization, tenant isolation, integrity, privacy, resume, and idempotency coverage
- EF Core migrations `20260810003349_AddDesktopCatalogScanCandidates` and `20260810003907_AddDesktopCatalogScans` are applied to the local database
- EF Core pending-model check: no pending changes
- macOS release build: 50.1 MB build report (48 MiB on disk), universal `x86_64` + `arm64` app; ad hoc signed and strictly validated with sandbox user-selected read/write access
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

Replace development-only control-plane startup and filesystem-storage assumptions with a versioned deployable prototype environment and a production-object-storage adapter behind the existing hosted-sync contract.

The design must satisfy all of these acceptance boundaries:

1. Preserve the existing desktop hosted-sync protocol, tenant authorization, resumable offset semantics, idempotent completion, immutable local package, and append-only local retry journal.
2. Add an object-storage abstraction/adapter suitable for the selected deployable prototype provider. Keep the controlled filesystem backend for local development and automated tests.
3. Derive object keys only from authorized organization/venue IDs, bounded package IDs, and validated logical paths. Never accept a client storage root or local path.
4. Define credential and secret boundaries, least-privilege runtime identity, environment-specific configuration validation, and fail-closed startup behavior. Commit no secrets.
5. Define safe begin/chunk/commit behavior for object storage, including concurrent/idempotent requests, incomplete multipart or temporary objects, checksum verification, final receipt publication, cleanup, and retry recovery.
6. Add versioned deployment configuration and migration/runbook material for database migrations, storage provisioning, health checks, rollback, and moving controlled filesystem packages if required.
7. Add automated adapter contract tests for authorization isolation, resume, duplicate chunks, tamper, incomplete commit, concurrency, link/path analogues, and unavailable storage. Use emulation or disposable controlled infrastructure by default.
8. Record local-development parity and a bounded deployed synthetic proof before claiming production-like execution. Do not claim regional durability, retention compliance, notarization, Windows readiness, or venue readiness without direct evidence.

The installed synthetic Save → hosted sync → Restore drill is complete. Deployable infrastructure and production-object-storage semantics are now the next bounded slice.

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
- `docs/LOCAL_ATTENDED_RESTORE.md` — offline attended restore, staging, verification, cleanup, evidence, and limitations
- `docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md` — customer identity, licensing, subscription, portal, and Admin-console structure
- `docs/SYSTEM_INVENTORY_PLUGIN.md` — direct app scan versus legacy Agent compatibility
- `docs/AUTOMATIC_DISCOVERY.md` — discovery and identification safety decisions
- `docs/INTEGRATION_CATALOG.md` — authoritative catalog/testing matrix
- `docs/adr/` — architecture decisions
- `services/contracts/`, `services/api/`, `services/platform/` — control-plane authority
- `apps/showvault_app/` — customer desktop implementation
