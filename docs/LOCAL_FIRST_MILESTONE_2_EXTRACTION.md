# Local-first milestone 2 extraction manifest

## Outcome

Milestone 2 reconstructs the local ShowVault Pro vault, offline immutable Save
and verification, exact native access authorization, and restart rehydration on
the then-current integrated milestone-1 branch.

This is local integration planning only. It does not authorize a push, PR
operation, merge, workflow dispatch, external equipment, personal-data access,
or venue use.

The customer outcome is:

**Scan → review → Save or Cancel → Verify locally → retain offline → reopen vault**

Cloud availability and authentication must not be prerequisites for those local
steps. Local verification and cloud synchronization remain separate states.

## Historical source boundary

The primary source range is the six commits `ce5be25..c172e49`:

| Commit | Historical concern |
| --- | --- |
| `bc53f4b` | Local vault foundation and Agent compatibility layout |
| `d424bb5` | Local-first product directive |
| `85b3e92` | Offline desktop Save and immutable recovery point |
| `ec92f08` | Offline Save handoff and runbook |
| `07e6e62` | Native authorization and vault rehydration |
| `c172e49` | Local synchronization handoff boundary |

These commits are source material, not a cherry-pick sequence. The range has 36
net-changed files and no transient net-zero files. Fourteen files overlap the
paused legacy slice, 22 do not, and 14 also changed during milestone 1. Eight
files are introduced in this range:

- `apps/showvault_app/lib/src/recovery/local_access_coordinator.dart`
- `apps/showvault_app/lib/src/recovery/local_recovery_service.dart`
- `apps/showvault_app/test/recovery/local_access_coordinator_test.dart`
- `apps/showvault_app/test/recovery/local_recovery_service_test.dart`
- `docs/LOCAL_DESKTOP_SAVE.md`
- `docs/LOCAL_FIRST_PRODUCT_BIBLE.md`
- `services/agent/src/ShowVault.Agent/Recovery/LocalVaultLayout.cs`
- `services/agent/tests/ShowVault.Agent.Tests/LocalVaultLayoutTests.cs`

The final behavior also requires the later published correction `ddfcaa6`.
Configured legacy Agent package storage must not resolve the default Documents
vault when that vault is unused or unavailable. This carry-forward is mandatory
even though its commit lies outside the historical milestone-2 range.

## Reconstruction order

Build the milestone as five reviewable commits. Split mixed files by the concern
below; do not replay their complete historical diff.

### 1. Opaque candidate-key Save contract

Reconstruct:

- the candidate-key additions in
  `services/api/src/ShowVault.Api/Contracts/RecoveryCandidateContracts.cs`;
- the matching newest-direct-scan projection in
  `services/api/src/ShowVault.Api/Endpoints/RecoveryCandidateEndpoints.cs`;
- only the candidate-key privacy/authorization cases in
  `services/api/tests/ShowVault.Api.Tests/AgentEnrollmentTests.cs`;
- the decoding fields in `apps/showvault_app/lib/src/api/showvault_api.dart`.

The API returns the already stored opaque allowlisted key to the authorized
desktop. It must not accept or return a source path, and it must not infer a Save
root from client-provided product text. Direct detections remain detection-only
until the local desktop independently resolves an exact allowlisted
`UserDataRoot` key.

### 2. Immutable offline Save engine

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/local_recovery_service.dart`;
- the Save portions of
  `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart`;
- the matching catalog resolution in
  `apps/showvault_app/lib/src/scanning/local_catalog_scanner.dart`;
- `apps/showvault_app/test/recovery/local_recovery_service_test.dart`;
- the matching Save cases in `apps/showvault_app/test/app_test.dart` and
  `apps/showvault_app/test/scanning/local_catalog_scanner_test.dart`;
- `crypto` in `apps/showvault_app/pubspec.yaml` and the resolved lockfile.

Required behavior:

- accept only an exact catalog-defined `UserDataRoot` after explicit
  confirmation;
- reject installed-application candidates as Save roots;
- reject root/descendant links, unsafe logical paths, unsupported entries,
  mutation, count/size/time limits, and cancellation;
- stream SHA-256 during copy and independently rehash staged content;
- create a new immutable recovery point and never overwrite a prior one;
- atomically publish matching package and independent manifests;
- write exactly one idempotent queue record only after verification passes; and
- publish no package or queue job on any failure.

Exact source paths may exist only in protected local recovery metadata where
restore requires them. They must not enter scan submissions, cloud-facing
manifests, queue diagnostics, logs, or control-plane evidence.

### 3. Native access authorization and restart rehydration

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/local_access_coordinator.dart`;
- `apps/showvault_app/test/recovery/local_access_coordinator_test.dart`;
- the rehydration portions of
  `apps/showvault_app/lib/src/recovery/local_recovery_service.dart` and its test;
- the **Open local vault** portions of
  `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart` and app tests;
- `SHOWVAULT_SYNTHETIC_FIXTURE_HOME` in
  `apps/showvault_app/lib/src/config/app_config.dart`;
- `file_selector` in `apps/showvault_app/pubspec.yaml` and its lockfile;
- macOS user-selected read/write entitlements and generated plugin registrant;
- Windows generated plugin registrant and plugin CMake list.

Required behavior:

- source authorization must canonicalize to the exact catalog-approved root;
- vault authorization is explicit, configurable, and session-scoped;
- no persistent security-scoped bookmark or broad filesystem grant is stored;
- relative, missing, linked, substituted, or mismatched selections fail closed;
- synthetic mode redirects only catalog fixture roots, suppresses real installed
  application candidates, and is absent from normal builds;
- vault opening reads only bounded ShowVault-owned manifests and queue records;
- independent-manifest filename/hash identity and package-manifest equality are
  reverified before local status is restored; and
- restart rehydration does not rescan or require the source.

Generated plugin files must be regenerated by Flutter dependency tooling on the
integration base and reviewed, not copied blindly from historical output.

### 4. Legacy Agent local-vault compatibility

Reconstruct only the retained compatibility behavior in:

- `services/agent/src/ShowVault.Agent/AgentOptions.cs`;
- `services/agent/src/ShowVault.Agent/AgentWorker.cs`;
- `services/agent/src/ShowVault.Agent/Execution/AgentCommandExecutor.cs`;
- `services/agent/src/ShowVault.Agent/Program.cs`;
- `services/agent/src/ShowVault.Agent/Queue/AgentQueueStore.cs`;
- `services/agent/src/ShowVault.Agent/Recovery/LocalVaultLayout.cs`;
- `services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageVerifier.cs`;
- `services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageWriter.cs`;
- `services/agent/src/ShowVault.Agent/appsettings.json`;
- `services/agent/tests/ShowVault.Agent.Tests/AgentCommandExecutorTests.cs`;
- `services/agent/tests/ShowVault.Agent.Tests/LocalVaultLayoutTests.cs`.

The compatibility layout creates only the canonical ShowVault-owned folders and
places the SQLite queue in `Upload Queue` by default. Legacy configured package
directories remain supported. In `RecoveryPackageWriter`, apply the final
`ddfcaa6` conditional construction behavior so explicit package-directory mode
does not construct `LocalVaultLayout` or require a default Documents vault. A
verified package queues once; failed verification does not queue.

This code does not authorize customer Agent installation, enrollment, service
setup, or Keychain use. The desktop JSON queue and legacy Agent SQLite queue
remain separate implementations pending a later packaged-engine consolidation.

### 5. Current product contract and runbooks

Reconcile:

- `docs/LOCAL_FIRST_PRODUCT_BIBLE.md` as the product authority;
- `docs/LOCAL_DESKTOP_SAVE.md` as the bounded operator/data contract;
- `docs/PROTOTYPE_READINESS.md`;
- `README.md`.

Regenerate `CHAT_CONTINUATION_README.md` only after code verification. Do not
carry stale cloud, personal-equipment, installed-app, or readiness claims into a
new integration branch without matching evidence.

## Complete file accounting

The 36 net files divide into:

- 3 server API contract/endpoint/test files;
- 17 desktop source (including the API client), native registration, dependency,
  and test files;
- 11 legacy Agent compatibility source/test files; and
- 5 repository/product/runbook files.

Reproduce the source accounting from the repository root:

```bash
test "$(git rev-list --count ce5be25..c172e49)" = 6
test "$(git diff --name-only ce5be25..c172e49 | sort -u | wc -l | tr -d ' ')" = 36

legacy_files="$(mktemp)"
milestone_files="$(mktemp)"
milestone_1_files="$(mktemp)"
git diff --name-only 254cbbf..310190c | sort -u > "$legacy_files"
git diff --name-only ce5be25..c172e49 | sort -u > "$milestone_files"
git diff --name-only 310190c..ce5be25 | sort -u > "$milestone_1_files"
test "$(comm -12 "$legacy_files" "$milestone_files" | wc -l | tr -d ' ')" = 14
test "$(comm -13 "$legacy_files" "$milestone_files" | wc -l | tr -d ' ')" = 22
test "$(comm -12 "$milestone_1_files" "$milestone_files" | wc -l | tr -d ' ')" = 14
```

Temporary files are outside the repository and may be discarded through normal
temporary-file cleanup. Do not broaden cleanup beyond those exact files.

## Verification gate

After reconstruction, run at minimum:

```bash
cd apps/showvault_app
flutter pub get
flutter analyze
flutter test test/recovery/local_recovery_service_test.dart \
  test/recovery/local_access_coordinator_test.dart \
  test/scanning/local_catalog_scanner_test.dart test/app_test.dart
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

Inspect the release entitlements and final diff for exact-path leakage,
unbounded filesystem access, persistent grants/bookmarks, link traversal,
mutable publication, queue-before-verification, source rescanning during vault
open, personal-Keychain calls, unguarded synthetic configuration, cloud-required
local actions, and customer-facing Agent setup.

Use synthetic fixtures only unless separate controlled-equipment authorization
is granted. Passing this gate does not establish attended picker behavior,
Windows runtime readiness, distribution signing/notarization, clean-machine
support, personal-data safety, venue readiness, or Recovery Confidence.
