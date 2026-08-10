# Local-first milestone 3 extraction manifest

## Outcome

Milestone 3 reconstructs verified-only durable synchronization, the authenticated
hosted transport/protocol, and attended offline Restore on the then-current
integrated milestone-2 branch.

This document authorizes local planning only. It does not authorize a push, PR
operation, merge, workflow dispatch, external equipment, personal data, or venue
use.

The customer outcome is:

**Verified locally → synchronize when available → restore while offline**

Synchronization failure must never weaken or delete the local recovery point.
Restore must not depend on login, cloud availability, or upload completion.

## Historical source boundary

Use the complete ten-commit range `c172e49..fff4434`:

| Commit | Historical concern |
| --- | --- |
| `f016ad1` | Durable local synchronization executor and synthetic substitute |
| `378acce` | Synchronization handoff |
| `36fcda9` | Attended verified local Restore |
| `e8819cc` | Restore handoff |
| `5f05f44` | Authenticated hosted synchronization client and API |
| `e980165` | Installed-drill starting handoff |
| `a62649f` | Sandbox-safe selected-target Restore correction |
| `97b56a0` | Installed hosted recovery evidence |
| `a7eee0d` | Immediate synchronized-status refresh correction |
| `fff4434` | Installed-drill final handoff |

Do not stop at `e980165`: the sandbox staging and status-refresh defects are
known product issues fixed by `a62649f` and `a7eee0d`. Reconstruct their final
behavior from the first integration draft.

The range has 31 net-changed files and no transient net-zero files. Four files
overlap the paused legacy slice, seven overlap milestone 1, ten overlap milestone
2, and 18 files are introduced in this range.

## Reconstruction order

Build the milestone as four code commits plus current documentation. Mixed files
must be split by concern rather than replayed wholesale.

### 1. Verified-only local synchronization engine

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/local_package_verifier.dart`;
- `apps/showvault_app/lib/src/recovery/local_sync_object_store.dart`;
- `apps/showvault_app/lib/src/recovery/local_sync_service.dart`;
- synchronization-state support in
  `apps/showvault_app/lib/src/recovery/local_recovery_service.dart`;
- test-only synchronization configuration in
  `apps/showvault_app/lib/src/config/app_config.dart`;
- `apps/showvault_app/test/recovery/local_sync_service_test.dart`;
- only the local synchronization portions of
  `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart` and
  `apps/showvault_app/test/app_test.dart`.

Required behavior:

- consume only verified recovery points from the immutable queue intent;
- reopen the authorized vault and independently reverify manifest identity,
  package equality, exact file set, sizes, SHA-256 values, and link safety before
  sending bytes;
- construct a closed remote manifest that excludes source/package/vault paths,
  credentials, tokens, unrestricted metadata, and contents;
- append bounded state events under `Upload Queue/State` without modifying the
  original queue intent;
- use capped attempts and exponential retry for unavailable transports while
  sending permanent local/remote integrity failures to Queue attention;
- resume each object from durable remote length, reject stale/conflicting
  offsets, verify remote checksums, and complete idempotently; and
- preserve the local verified package on every cancellation, retry, failure, or
  remote conflict.

The direct folder substitute is allowed only when both
`SHOWVAULT_SYNTHETIC_FIXTURE_HOME` and
`SHOWVAULT_SYNTHETIC_OBJECT_STORE_ROOT` are explicitly defined. Normal builds
must not expose it.

### 2. Attended offline Restore

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/local_restore_service.dart`;
- Restore-specific verification in
  `apps/showvault_app/lib/src/recovery/local_package_verifier.dart`;
- target authorization in
  `apps/showvault_app/lib/src/recovery/local_access_coordinator.dart`;
- `apps/showvault_app/test/recovery/local_restore_service_test.dart`;
- Restore cases in
  `apps/showvault_app/test/recovery/local_access_coordinator_test.dart`;
- only the Restore UI/cases in
  `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart` and
  `apps/showvault_app/test/app_test.dart`.

Required behavior includes the final `a62649f` correction:

- accept only a locally verified recovery point from the authorized vault;
- reverify both manifests and the exact immutable content tree before copying;
- accept an absent programmatic target or an operator-selected existing empty
  regular target, never a non-empty target, file, link, vault ancestor, or vault
  descendant;
- for a selected existing target, keep owned staging inside that authorized
  target and publish the fixed `ShowVault Restored Files` child;
- for an absent programmatic target, retain direct same-volume atomic
  publication;
- verify staged and published bytes and identities;
- remove only staging with an exact bounded ownership marker;
- preserve unrelated/unowned staging and all operator content;
- publish no partial completion after cancellation, timeout, mutation, target
  replacement, or interrupted restart; and
- write bounded path-free Restore evidence only after final verification.

Restore must remain available while signed out and must not load files into a
running application or live device.

### 3. Authenticated hosted transport and tenant protocol

Add or reconstruct the desktop client:

- `apps/showvault_app/lib/src/recovery/hosted_sync_object_store.dart`;
- `apps/showvault_app/test/recovery/hosted_sync_object_store_test.dart`;
- hosted-transport selection in
  `apps/showvault_app/lib/src/recovery/local_sync_object_store.dart` and
  `apps/showvault_app/lib/src/recovery/local_sync_service.dart`.

Add or reconstruct the API protocol:

- `services/api/src/ShowVault.Api/Contracts/HostedSyncContracts.cs`;
- `services/api/src/ShowVault.Api/Endpoints/HostedSyncEndpoints.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedManifestValidator.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedSyncExceptions.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedSyncModels.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedSyncOptions.cs`;
- `services/api/src/ShowVault.Api/HostedSync/HostedSyncStore.cs`;
- the milestone-3 registration in `services/api/src/ShowVault.Api/Program.cs` and
  `services/api/src/ShowVault.Api/appsettings.json`;
- `services/api/tests/ShowVault.Api.Tests/HostedSyncTests.cs` and matching
  fixtures in `TenantApiFactory.cs`.

Required behavior:

- construct the hosted client only after an in-memory access token and exact
  organization/venue context are available;
- never persist tokens or hosted capabilities in the vault or queue;
- require manager/administrator/owner membership for the route tenant and deny
  missing, viewer, outsider, and cross-tenant access;
- validate the complete closed manifest and approved catalog metadata before
  accepting chunks;
- derive storage paths only from authorized GUIDs, bounded package identity, and
  validated logical paths;
- freeze the begun manifest, reject gaps/conflicts/extra files/links/tamper, and
  independently hash all completed objects;
- make duplicate chunks and concurrent/idempotent completion safe; and
- write the receipt last as the only completion marker.

The historical milestone-3 backend is a configured server-owned filesystem
prototype. During staged integration it must be Development/test-only, or
production startup must fail closed until milestone 4 supplies the reviewed
S3-compatible provider abstraction. Never make an arbitrary filesystem root a
production storage option.

### 4. Immediate UI state reconciliation

Integrate `a7eee0d` into the dashboard/synchronization commit rather than as a
follow-up. After synchronization, candidate chips must prefer the newly
rehydrated vault record so local/cloud status updates immediately without an app
restart. Preserve local findings and all failure states; do not manufacture a
synchronized status from a successful HTTP response alone.

### 5. Current runbooks and evidence limits

Reconcile:

- `docs/LOCAL_QUEUE_SYNC.md`;
- `docs/LOCAL_ATTENDED_RESTORE.md`;
- `docs/LOCAL_DESKTOP_SAVE.md`;
- `docs/PROTOTYPE_READINESS.md`;
- `README.md`.

Regenerate `CHAT_CONTINUATION_README.md` only after verification. Historical
installed macOS evidence may document the defects and their corrections, but it
must not be presented as evidence for a reconstructed branch unless rerun there.

## Complete file accounting

The 31 net files divide into:

- 14 desktop source/test files;
- 11 API source/config/test files; and
- 6 repository/runbook files.

Reproduce the accounting from the repository root:

```bash
test "$(git rev-list --count c172e49..fff4434)" = 10
test "$(git diff --name-only c172e49..fff4434 | sort -u | wc -l | tr -d ' ')" = 31

legacy_files="$(mktemp)"
milestone_1_files="$(mktemp)"
milestone_2_files="$(mktemp)"
milestone_3_files="$(mktemp)"
git diff --name-only 254cbbf..310190c | sort -u > "$legacy_files"
git diff --name-only 310190c..ce5be25 | sort -u > "$milestone_1_files"
git diff --name-only ce5be25..c172e49 | sort -u > "$milestone_2_files"
git diff --name-only c172e49..fff4434 | sort -u > "$milestone_3_files"
test "$(comm -12 "$legacy_files" "$milestone_3_files" | wc -l | tr -d ' ')" = 4
test "$(comm -12 "$milestone_1_files" "$milestone_3_files" | wc -l | tr -d ' ')" = 7
test "$(comm -12 "$milestone_2_files" "$milestone_3_files" | wc -l | tr -d ' ')" = 10
test "$(git diff --diff-filter=A --name-only c172e49..fff4434 | wc -l | tr -d ' ')" = 18
```

Temporary accounting files may be discarded through normal temporary-file
cleanup. Do not broaden cleanup beyond those exact files.

## Verification gate

After reconstruction, run at minimum:

```bash
cd apps/showvault_app
flutter analyze
flutter test test/recovery/local_sync_service_test.dart \
  test/recovery/hosted_sync_object_store_test.dart \
  test/recovery/local_restore_service_test.dart \
  test/recovery/local_access_coordinator_test.dart test/app_test.dart
flutter test
flutter build macos --release

cd ../..
dotnet test services/contracts/tests/ShowVault.AgentContracts.Tests/ShowVault.AgentContracts.Tests.csproj
dotnet test services/platform/tests/ShowVault.Platform.Tests/ShowVault.Platform.Tests.csproj
dotnet test services/agent/tests/ShowVault.Agent.Tests/ShowVault.Agent.Tests.csproj
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project services/api/src/ShowVault.Api/ShowVault.Api.csproj \
  --startup-project services/api/src/ShowVault.Api/ShowVault.Api.csproj
dotnet test services/api/tests/ShowVault.Api.Tests/ShowVault.Api.Tests.csproj
git diff --check
```

Audit the final diff and evidence for token/path leakage, client-supplied tenant
or storage roots, unbounded request bodies, manifest mutation, queue state
replacement, retry deletion, link traversal, cross-tenant access, unsafe restore
containment, sibling staging outside the selected sandbox, broad cleanup,
cloud-required Restore, synthetic substitute exposure, and unsafe production
filesystem storage.

Use synthetic fixtures by default. Passing this gate does not establish
production-provider durability, distribution signing, Windows installed
behavior, attended Auth0, clean-machine support, personal-data safety, venue
readiness, dependency closure, or Recovery Confidence.
