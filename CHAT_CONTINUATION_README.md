# ShowVault active continuation handoff

Read this file completely at the start of a new ShowVault development chat. It is the concise authority for the current state and next task. Repository code, contracts, tests, migrations, ADRs, and Git history remain authoritative when more specific.

## Product direction

ShowVault is a recovery-first venue-resilience platform:

**Scan → Backup → Verify → Restore**

The customer desktop experience must be simple:

**Install → Sign in → Scan this computer**

There is no customer-facing Agent installation, enrollment code, service setup, or personal-Keychain workflow. The desktop app must not persist scan results or backup packages locally. Exact source paths may exist only transiently in memory while the app checks catalog entries or streams an authorized backup directly to cloud storage. The control plane receives path-free metadata.

The implementation remains venue-neutral and cross-platform. LIV nightclub is the intended first venue deployment, not a source of build-time assumptions or current test data. Use only the Product Owner's Mac and other explicitly authorized controlled equipment until the readiness gates pass.

## Current repository state

- Repository: `/Users/infamous/Documents/ChatGPT/showvault`
- Branch: `codex/personal-catalog-scan-beta`
- Feature commit: `3ed4bdc feat: scan computers directly without agent enrollment`
- Expected worktree after the handoff commit: clean except intentionally untracked `NEXT_CONVERSATION.md`
- Sequential catalog expansion remains paused while prototype readiness advances.

## Completed outcome

- The native app now has a single uncluttered customer screen: **This computer**, **Cloud connected**, **Scan this computer**, detected systems, and sign out.
- The visible Agent installation, one-time enrollment, Venue Agent selector, recovery workflow controls, placeholder navigation, search, and notification controls are removed.
- macOS and Windows Auth0 sessions are held in application memory only. ShowVault does not write its operator session to the macOS login Keychain; relaunch requires sign-in.
- `LocalCatalogScanner` checks only exact catalog-defined candidates. It does not enumerate installed applications, directories, disks, networks, machine identity, or file contents.
- The current direct beta registry checks Resolume Arena and Serato DJ Pro application/user-data candidates on macOS and Windows.
- The app submits only opaque candidate keys to the authenticated manager-only `POST /api/v1/organizations/{organizationId}/venues/{venueId}/computer-scans` endpoint.
- The server independently allowlists every accepted key and maps it to bounded product/type/evidence metadata. Unknown keys, paths, and oversized requests are rejected.
- Direct scan headers and candidates are stored in the cloud database. An empty newer scan correctly supersedes older results.
- Direct detections appear as **Detected** and cannot enter the legacy Agent approval/backup controls.
- Legacy Agent protocol 1.21 remains in the repository only as compatibility infrastructure; it is not part of the customer desktop onboarding flow.
- Product documentation now states that the future backup path must stream source bytes directly to cloud storage without a local backup package or local scan database.

## Installed personal-Mac evidence

- Final app artifact: `/tmp/showvault-macos-direct-scan-beta-20260809-v5/ShowVault.app`
- Transfer ZIP: `/tmp/showvault-macos-direct-scan-beta-20260809-v5/ShowVault-macos.zip`
- ZIP SHA-256: `dd7fbb4acca83b54170d62add17e0bc23ce0c5fefaa3145270fea7ed5e2ef716`
- App version/build: `0.1.0 (1)`
- Bundle ID: `com.showvault.app`
- Architecture: universal `x86_64` + `arm64`
- Signing: ad hoc, not notarized; personal attended testing only
- Test Mac: macOS 26.3 build 25D125
- Control plane: local Production-mode API at `http://127.0.0.1:5000` backed by local PostgreSQL
- Final verified scan ID: `bfa3b857-9a7e-46fb-9bbb-a800599c268a`
- Scan result: completed with three candidates—Resolume Arena installed application, Serato DJ Pro installed application, and Serato DJ Pro user-data root
- Database privacy check: candidate count 3; no `/` separator in any stored candidate key or evidence field
- No Keychain, filesystem permission, or security prompt appeared during the final direct scan.

Do not copy exact local source paths into control-plane evidence or future documentation. The test above intentionally records only path-free product/type results.

## Verification baseline

- Flutter analysis: no issues
- Flutter tests: 26 passed
- Contracts tests: 2 passed
- Agent tests: 426 passed
- Platform tests: 28 passed
- API tests: 8 passed
- EF Core migrations `20260810003349_AddDesktopCatalogScanCandidates` and `20260810003907_AddDesktopCatalogScans` are applied to the local database
- EF Core pending-model check: no pending changes
- macOS release build: 49.0 MB universal app
- ZIP checksum matches `SHA256SUMS`
- `git diff --check`: passes

## Safety boundaries

- Never request or approve access to the user's personal login Keychain for ShowVault.
- Never store Auth0 credentials, tokens, enrollment codes, client secrets, exact source paths, scan databases, or backup packages on the customer computer.
- Never enumerate unrelated installed applications, arbitrary directories, disks, networks, or machine identity.
- Detection is not backup, verification, restore, or recovery readiness. Preserve those states separately.
- Do not weaken Gatekeeper or system-wide security for the ad hoc personal-test build.
- Do not claim notarization, clean-machine installation, Windows runtime readiness, venue readiness, or complete recovery readiness without direct evidence.
- Do not use venue equipment, networks, credentials, paths, topology, or data without new explicit authorization.

## Exact next bounded objective

Implement the smallest direct-to-cloud backup vertical slice for a detected **UserDataRoot** candidate, beginning with Serato DJ Pro on the controlled personal Mac.

The design must satisfy all of these acceptance boundaries:

1. The desktop app resolves the exact catalog path only in memory after the user explicitly starts backup.
2. The API authorizes the tenant, venue, candidate, and backup attempt and issues only short-lived, least-privilege upload capability; do not add an Agent/enrollment dependency.
3. The app streams files directly from the exact allowlisted root to cloud object storage. It must not create a local archive, staging directory, scan database, resume database, or plaintext manifest.
4. Cloud metadata must use normalized relative logical names, hashes, sizes, and bounded status—not absolute local paths.
5. Apply strict containment, symlink, file-count, per-file-size, total-byte, timeout, cancellation, and mutation/error rules before reading contents.
6. Do not read file contents during detection. Content reads begin only after the explicit backup action and authorization.
7. The first slice must remain venue-neutral and use only synthetic fixtures plus the Product Owner's explicitly authorized personal Serato data when runtime testing is reached.
8. Add tests for tenant authorization, key allowlisting, containment, path privacy, no local artifacts, interrupted uploads, and an empty/changed source.
9. Do not claim verification or restore until independently implemented and proven.

Before implementation, inspect existing backup/object-storage contracts and ADRs, then choose the smallest compatible upload design. If the existing storage layer cannot safely support direct streaming, document the precise gap and implement only the prerequisite bounded contract rather than inventing credentials or local staging.

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
- `docs/SYSTEM_INVENTORY_PLUGIN.md` — direct app scan versus legacy Agent compatibility
- `docs/AUTOMATIC_DISCOVERY.md` — discovery and identification safety decisions
- `docs/INTEGRATION_CATALOG.md` — authoritative catalog/testing matrix
- `docs/adr/` — architecture decisions
- `services/contracts/`, `services/api/`, `services/platform/` — control-plane authority
- `apps/showvault_app/` — customer desktop implementation
