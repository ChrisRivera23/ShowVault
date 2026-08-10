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
- Branch: `codex/local-first-vault`
- Local-first vault foundation commit: `bc53f4b feat: establish local-first vault foundation`
- Offline desktop Save commit: `85b3e92 feat: save desktop recovery points offline`
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
- A passing local structural/cryptographic verification creates exactly one idempotent queued cloud-upload job. Failed or unverified packages are not queued; synchronization execution is not implemented yet.
- The Flutter dashboard keeps Scan and Save usable while signed out or the API is unavailable. Cloud submission is best-effort and cannot erase local findings.
- An explicit desktop Save resolves only an opaque allowlisted `UserDataRoot` key after confirmation, rejects links and unsafe entries, enforces count/size/timeout/cancellation/mutation rules, streams and independently verifies SHA-256 content, publishes atomically, and writes a JSON upload-queue job only after verification.
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

## Verification baseline

- Flutter analysis: no issues
- Flutter tests: 36 passed
- Contracts tests: 2 passed
- Agent tests: 429 passed
- Platform tests: 28 passed
- API tests: 15 passed, including exact beta-token and Development/configuration/loopback guard coverage
- EF Core migrations `20260810003349_AddDesktopCatalogScanCandidates` and `20260810003907_AddDesktopCatalogScans` are applied to the local database
- EF Core pending-model check: no pending changes
- macOS release build: 49.6 MB universal `x86_64` + `arm64` app; ad hoc signed
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

Implement explicit macOS and Windows permission onboarding for the exact source and configurable vault, then rehydrate local recovery/queue status after app restart. Use synthetic fixtures and folders only.

The design must satisfy all of these acceptance boundaries:

1. Explain source and vault access before invoking a native operating-system permission surface.
2. Accept only a selected source whose canonical identity matches the exact catalog-approved `UserDataRoot`; reject substitutions, aliases, and links.
3. Let the operator confirm the default Documents vault or choose a configurable vault without exposing infrastructure concepts.
4. Retain only the minimum platform permission/bookmark state required for later authorized access; never place credentials or secrets in the vault.
5. Read no source contents before explicit Save confirmation and successful permission validation.
6. On restart, inspect only ShowVault-owned manifests and queue records, then rehydrate local verification and cloud-queue status without rescanning source contents.
7. Keep macOS and Windows behavior behind one tested Dart contract with platform-specific permission adapters.
8. Prove the installed macOS flow using an explicitly authorized synthetic source and vault only; do not touch personal application data.
9. Do not claim cloud synchronization, dependency closure, Recovery Confidence, Windows runtime readiness, or personal-data readiness until separately proven.

The installed desktop Save action is now connected in code and proven with synthetic tests. It creates `Backups/<product>/<UTC timestamp>__<immutable ID>`, a local manifest and summary, an independent manifest copy, and an atomic verified-only JSON queue record. The UI shows local verification separately from cloud queue state and remains usable without cloud connectivity. The next slice must add operating-system permission onboarding and restart rehydration before any installed personal-data Save drill. Cloud synchronization execution remains unimplemented.

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
- `docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md` — customer identity, licensing, subscription, portal, and Admin-console structure
- `docs/SYSTEM_INVENTORY_PLUGIN.md` — direct app scan versus legacy Agent compatibility
- `docs/AUTOMATIC_DISCOVERY.md` — discovery and identification safety decisions
- `docs/INTEGRATION_CATALOG.md` — authoritative catalog/testing matrix
- `docs/adr/` — architecture decisions
- `services/contracts/`, `services/api/`, `services/platform/` — control-plane authority
- `apps/showvault_app/` — customer desktop implementation
