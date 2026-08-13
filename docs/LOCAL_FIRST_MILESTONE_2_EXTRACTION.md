# Local-first milestone 2 extraction and architecture contract

## Outcome

Milestone 2 adds the first bounded desktop **Save** path after milestone 1's
direct Scan:

**Scan → select a detected user-data source → Save or Cancel → verify locally → retain an immutable recovery point → queue a verified copy**

It remains usable without login or network access. It does not upload anything,
restore anything, calculate Recovery Confidence, access personal data during
tests, or expose Agent installation/enrollment/service controls.

This document authorizes no implementation or external action by itself.

## Historical source boundary

The source-material range is `ce5be25..c172e49`, exactly six commits:

| Commit | Historical concern | Disposition |
| --- | --- | --- |
| `bc53f4b` | Agent-local vault layout and SQLite queue placement | extract concepts only |
| `d424bb5` | local-first product direction | retain and reconcile |
| `85b3e92` | Dart desktop Save, manifest, verification, JSON queue | replace/narrow |
| `ec92f08` | offline-Save handoff | regenerate |
| `07e6e62` | native directory authorization and vault rehydration | replace/narrow |
| `c172e49` | synchronization handoff | regenerate |

The range is 36 net paths, `+2,677/-220`, binary-diff SHA-256
`8159a89c6ec60da7637c763833937245a51bb1b8dde7a166aeb74b850ad3f9c1`,
and path-list SHA-256
`efa196632c912c61d674a71a2bfc592880d7fa674b6b3eaf11a2a6fe7d800daa`.
Twelve paths overlap milestone 1 and must be reconciled, never transplanted:

- `CHAT_CONTINUATION_README.md`
- `README.md`
- `apps/showvault_app/lib/src/api/showvault_api.dart`
- `apps/showvault_app/lib/src/config/app_config.dart`
- `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart`
- `apps/showvault_app/lib/src/scanning/local_catalog_scanner.dart`
- `apps/showvault_app/test/app_test.dart`
- `apps/showvault_app/test/scanning/local_catalog_scanner_test.dart`
- `docs/PROTOTYPE_READINESS.md`
- `services/api/src/ShowVault.Api/Contracts/RecoveryCandidateContracts.cs`
- `services/api/src/ShowVault.Api/Endpoints/RecoveryCandidateEndpoints.cs`
- `services/api/tests/ShowVault.Api.Tests/AgentEnrollmentTests.cs`

Reproduce the accounting with:

```bash
test "$(git rev-list --count ce5be25..c172e49)" = 6
test "$(git diff --name-only ce5be25..c172e49 | sort -u | wc -l | tr -d ' ')" = 36
test "$(git diff --binary ce5be25..c172e49 | shasum -a 256 | cut -d' ' -f1)" = \
  8159a89c6ec60da7637c763833937245a51bb1b8dde7a166aeb74b850ad3f9c1
```

## Why the historical implementation cannot be replayed

The old Dart engine is useful product evidence, but it is not a safe current
implementation boundary:

- it discovers paths, then reopens them by pathname without retained
  directory/file identities, leaving source and vault link/swap races;
- it does not reject source/vault equality, nesting, or overlap;
- it creates vault subdirectories without rejecting pre-existing linked or
  non-directory components;
- it publishes the package before the independent manifest and queue record,
  and can enqueue even after independent-manifest persistence fails;
- its queue record stores an absolute package path instead of a vault-relative
  identity;
- vault rehydration compares manifests but does not rehash package content
  before restoring `verified` state;
- its path-based JSON queue does not provide one transactional, restart-safe
  state machine for attempt/error transitions; and
- it duplicates local recovery behavior between Flutter and the legacy Agent.

The approved disposition is **replace/narrow**.

## Architectural decision: one packaged local engine

Filesystem capture, verification, vault layout, and queue persistence belong in
one venue-neutral .NET local-engine library with a narrow packaged desktop host.
The Flutter app remains the customer UI and native directory-consent surface.
The host is an internal ShowVault component, not a separately installed or
enrolled Agent, and exposes no arbitrary shell, network, or Agent protocol.

Extract reusable hardened recovery primitives from the current Agent where
appropriate; do not make the customer application depend on Agent identity,
command queues, enrollment credentials, control-plane connectivity, or Agent
service lifecycle.

The local host contract accepts only closed operations and bounded records:

- initialize/inspect an explicitly authorized vault;
- authorize and save one exact catalog `UserDataRoot` candidate;
- cancel a running Save; and
- list locally verified recovery-point summaries.

It must not accept arbitrary command names, plugin assemblies, network targets,
or unrestricted paths. Paths are local-process inputs only and never appear in
cloud-facing messages, logs, or UI errors.

## Reconstruction order

### 1. Local-engine contracts and catalog authorization

Create a small shared contract for opaque candidate key, exact selected source,
explicit selected vault, bounds, cancellation, progress, and path-free result.

Required behavior:

- Save is available only for a milestone-1 detection whose type is
  `UserDataRoot` and whose opaque key exists in the same closed catalog;
- the source picker must select the exact current catalog candidate;
- the vault picker is independent and session-scoped;
- selected source and vault must be distinct, non-overlapping canonical trees;
- neither selected root nor any existing component may be a filesystem link,
  reparse point, mount substitution, or non-directory entry;
- no persistent bookmark or broad filesystem grant is introduced; and
- installed-application detections remain detection-only.

### 2. Canonical vault and transactional queue store

Create and validate the configurable vault layout:

```text
ShowVault Pro/
├── Backups/
├── Manifests/
├── Device Exports/
├── Upload Queue/
├── Reports/
├── Logs/
└── Quarantine/
```

Use one SQLite database under `Upload Queue` for recovery-point publication
state and future upload attempts. Enable foreign keys, WAL, a bounded busy
timeout, and an explicit durability mode. Schema creation/migration must be
transactional and idempotent. Records identify packages relative to the
authorized vault; no source path, credential, token, or private content enters
the queue.

State transitions are closed and conditional, at minimum:

`staging → verified → queued`, with explicit failed/cancelled repair states.

Only a fully published and freshly reverified recovery point may become
`queued`. Restart repair may recover or quarantine an orphan, but may never
invent verification success.

### 3. Stable bounded capture and immutable publication

Capture from retained no-follow directory/file identities rather than
rediscovering by pathname. Enforce before and throughout copying:

- directory, file-count, path-length, per-file, aggregate-byte, and duration
  limits;
- cancellation at enumeration, read, hash, verification, and publication
  boundaries;
- regular-file-only topology and normalized unique relative paths;
- no links, devices, sockets, aliases, reparse points, mount escapes, or hard
  links outside the retained authorized tree;
- stable root/directory/file identity, size, modification, and final topology;
- exact closure: every retained file appears once and no late entry is silently
  omitted; and
- staging and final paths contained under the retained authorized vault.

Build the complete recovery point in a same-filesystem staging directory.
Verify it before one non-overwriting atomic publication. A prior known-good
recovery point is never overwritten or deleted by Save failure.

### 4. Manifest and verification evidence

The immutable package contains:

```text
Backups/<product>/<UTC timestamp>__<manifest SHA-256>/
├── content/
├── manifest.json
├── verification.json
└── summary.txt
```

The manifest is deterministic, bounded, path-safe, and contains the opaque
candidate key, product/plugin identity, relative files, sizes, SHA-256 hashes,
and honest empty dependency/compatibility collections. It does not contain the
absolute source or vault path. License transfer, dependency closure,
compatibility, and recoverability are not claimed.

`verification.json` is written only after independent structural and streaming
cryptographic verification of the staged package. It binds the package ID,
manifest digest, exact verified file set, time, result, and evidence digest.
The independent copies under `Manifests` must match the package records before
queue insertion.

### 5. Flutter Save/Cancel and rehydration UI

Keep milestone 1 Scan and authentication behavior intact. Add only:

- Save on detected `UserDataRoot` findings;
- a plain confirmation before source/vault consent;
- Cancel while capture is running;
- bounded progress without exact paths;
- separate `Verified locally`, `Cloud queued`, and `Queue attention` states;
- `Open local vault` using explicit consent after restart; and
- path-free actionable errors.

Cloud/API failure must not block Scan, Save, verification, vault inspection, or
access to an existing verified recovery point. No upload executor or restore UI
belongs in this milestone.

### 6. Current documentation and handoff

Add the local-first product bible and current Save/vault documentation, then
regenerate readiness and continuation files from actual results. Historical
installed-Mac proof must remain historical and must not be claimed for the
reconstructed branch.

## Required adversarial tests

Use synthetic roots only. Cover at minimum:

- source/vault equality and both nesting directions;
- linked root, linked descendant, linked vault component, hard-link escape,
  unsupported entry, and platform-specific reparse/mount substitutions;
- source/root/directory/file identity swaps before and during copy;
- late file, removed file, changed bytes with stable size/time, duplicate or
  unsafe relative path, and incomplete enumeration;
- empty source and every count/size/path/time bound;
- cancellation at enumeration, copy, verification, pre-publication, and queue
  transition;
- existing identical and conflicting package identity;
- extra/missing/mutated package entries and manifest/verification mismatch;
- independent-manifest failure, SQLite failure, restart at every durable state,
  and orphan repair/quarantine;
- repeated Save/idempotency without overwrite or duplicate queue records;
- no source/vault path in result, queue, logs, or cloud-facing API payloads;
- signed-out/offline UI, failed optional API submission, and restart vault
  reopening without source rescan; and
- macOS/Windows behavior behind the same local-engine contract, with honest
  skips where native proof is unavailable.

## Verification gate

Run milestone-1 regression plus local-engine focused/full tests, Flutter focused
and full tests, Release builds for changed .NET projects, format checks, shell
and generated-plugin checks, and `git diff --check`. Inspect the complete diff
for path leakage, broad enumeration, content/network access outside Save,
personal-Keychain use, Agent customer-flow exposure, overwrite/delete behavior,
queue-before-verification, non-atomic publication, and native-proof overclaims.

Passing tests do not authorize push, PR mutation, workflow dispatch, native
packaging/installation, equipment, personal/customer/venue data, cloud
resources, release, deployment, or destructive cleanup.
