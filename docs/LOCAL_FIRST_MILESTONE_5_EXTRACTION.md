# Local-first milestone 5 extraction manifest

## Outcome

Milestone 5 reconstructs installed resilience automation, upgrade/reinstall vault
preservation, and explicit path-free support diagnostics on the then-current
milestone-4 branch.

This document authorizes local planning only. It does not authorize installed
execution, Docker mutation, external equipment, personal data, pushes, PR
operations, merges, workflow dispatch, distribution, or venue use.

The customer outcomes are:

- local recovery survives application replacement and ordinary removal;
- an operator can explicitly generate a bounded local diagnostic after opening
  a vault; and
- synthetic installed harnesses can exercise failure/restart behavior without
  exposing command mode in normal builds.

## Historical source boundary

Use the seven-commit range `69b83ab..3a5e715`:

| Commit | Historical concern |
| --- | --- |
| `c534af1` | Resilience-matrix starting handoff |
| `d744c03` | Installed synthetic resilience harness |
| `75a2586` | Recorded resilience evidence |
| `7acc3f3` | Upgrade/diagnostic starting handoff |
| `237f076` | Upgrade preservation and support diagnostics |
| `b9f0824` | Recorded upgrade/diagnostic evidence |
| `3a5e715` | Windows-packaging handoff boundary |

The range has 19 net files and no transient net-zero files. It adds nine files;
three overlap the paused legacy slice, seven overlap each of milestones 1, 2,
and 3, and three overlap milestone 4.

## Reconstruction order

Build the milestone as four reviewable code/harness commits plus current
runbooks. Do not mix test-only command paths with customer UI behavior.

### 1. Compile-time-gated resilience command mode

Add or reconstruct:

- the exact resilience flags and phase validation in
  `apps/showvault_app/lib/src/config/app_config.dart`;
- command routing in `apps/showvault_app/lib/main.dart`;
- `apps/showvault_app/lib/src/recovery/resilience_harness.dart`;
- normal-build disablement tests in
  `apps/showvault_app/test/config/app_config_test.dart`;
- only harness wiring in `apps/showvault_app/README.md`.

Command mode must require all of:

- `SHOWVAULT_RESILIENCE_HARNESS=true` at compilation;
- an opaque `showvault-resilience-*` synthetic fixture identity;
- a loopback HTTP API;
- guarded Development personal-beta authentication; and
- an exact allowlisted command and phase.

Normal builds compile the flag as false. Unknown identities, endpoints,
commands, or phases fail before fixture, vault, network, or application access.
The harness must not enumerate the host, inspect personal candidates, open the
personal Keychain, contact venue systems, or install a separate Agent.

### 2. Installed synthetic resilience runner

Add or reconstruct:

- `apps/showvault_app/tool/run-resilience-matrix.sh`;
- the resilience-only Development overrides in
  `infra/docker-compose.s3-test.yml`;
- path-free report generation in
  `apps/showvault_app/lib/src/recovery/resilience_harness.dart`.

Required behavior:

- require an absolute absent output directory;
- create only generated fixture bytes inside an isolated ShowVault sandbox;
- allocate a unique Compose project and loopback ports;
- build a release app with the explicit harness flag, then launch the copied
  executable as a fresh process for every phase;
- exercise API outage, partial upload interruption/resume, storage outage,
  idempotent completion, exact Restore, source mutation, local/remote tamper,
  incomplete/conflicting remote data, non-empty targets, and interrupted
  Restore;
- require every invalid path to preserve any valid local recovery point and
  publish neither a false receipt nor a partial Restore;
- emit only the copied app, ZIP, checksummed bounded report, and `SHA256SUMS`;
  and
- remove only ownership-marked synthetic state and the exact disposable Compose
  project.

The report excludes tokens, credentials, contents, exact paths, host/user
identity, and unrestricted inventory. Process restart is covered; host reboot is
not.

### 3. Explicit local support diagnostics

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/local_support_diagnostic_service.dart`;
- `apps/showvault_app/test/recovery/local_support_diagnostic_service_test.dart`;
- the confirmed operator action in
  `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart`;
- matching UI cases in `apps/showvault_app/test/app_test.dart`;
- any bounded inspection additions in
  `apps/showvault_app/lib/src/recovery/local_recovery_service.dart`.

Required behavior:

- expose **Create support diagnostic** only after an authorized vault is open;
- show the exact data boundary and require a second explicit confirmation;
- validate independent/package manifest equality and identity, queue intent,
  every bounded append-only queue event, and checksummed Restore evidence;
- reject links, substitutions, malformed/oversized records, unknown keys,
  identity mismatches, invalid checksums, and linked diagnostic destinations;
- never open recovery contents or recorded source paths;
- publish atomically under `Reports/Diagnostics` with a checksum sidecar; and
- never upload or transmit the diagnostic automatically.

The closed `showvault.support-diagnostic.v1` schema may contain only versions,
timestamps, counts, opaque package/candidate identities, bounded product/status
values, attempts/event counts, bounded error categories, integrity results, and
the report-core digest. It excludes raw errors, credentials, tokens, contents,
filenames inside packages, exact paths, host identity, network/application
inventory, and unrestricted logs.

### 4. Upgrade/reinstall preservation proof

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/upgrade_diagnostic_harness.dart`;
- `apps/showvault_app/tool/run-upgrade-diagnostic-proof.sh`;
- upgrade-generation test configuration in
  `apps/showvault_app/lib/src/config/app_config.dart` and its tests.

Required behavior:

- independently compile distinct before/after release applications;
- replace only a fixed installed application path;
- create, verify, queue/retry/synchronize, Restore, and diagnose a synthetic
  recovery point with the before app;
- delete the synthetic source, replace the app, explicitly reopen the unchanged
  external vault, and prove source-free rehydration with the after app;
- verify independent/package manifests, queue attempt/event history, Restore
  evidence, path-free diagnostics, executable/artifact hashes, and cleanup;
- retain the external vault during app replacement and ordinary app removal;
  and
- remove only owned synthetic proof state.

There is no destructive in-app full-data removal control. Rollback compatibility
is not proven by forward replacement and must fail without changing the vault
when an older app cannot understand its schema.

### 5. Current runbooks and evidence limits

Reconcile:

- `docs/INSTALLED_RESILIENCE_MATRIX.md`;
- `docs/UPGRADE_AND_SUPPORT_DIAGNOSTICS.md`;
- `docs/PROTOTYPE_READINESS.md`;
- `README.md` and `apps/showvault_app/README.md`.

Regenerate `CHAT_CONTINUATION_README.md` only after verification. Existing
macOS hashes/results remain historical controlled evidence and must not be
claimed for a reconstructed branch without rerunning the exact approved local
proof there.

## Complete file accounting

The 19 net files divide into:

- 13 application source/test/tool files;
- 1 disposable infrastructure override; and
- 5 repository/runbook files.

Reproduce the accounting from the repository root:

```bash
test "$(git rev-list --count 69b83ab..3a5e715)" = 7
test "$(git diff --name-only 69b83ab..3a5e715 | sort -u | wc -l | tr -d ' ')" = 19
test "$(git diff --diff-filter=A --name-only 69b83ab..3a5e715 | wc -l | tr -d ' ')" = 9

legacy_files="$(mktemp)"
milestone_1_files="$(mktemp)"
milestone_2_files="$(mktemp)"
milestone_3_files="$(mktemp)"
milestone_4_files="$(mktemp)"
milestone_5_files="$(mktemp)"
git diff --name-only 254cbbf..310190c | sort -u > "$legacy_files"
git diff --name-only 310190c..ce5be25 | sort -u > "$milestone_1_files"
git diff --name-only ce5be25..c172e49 | sort -u > "$milestone_2_files"
git diff --name-only c172e49..fff4434 | sort -u > "$milestone_3_files"
git diff --name-only fff4434..69b83ab | sort -u > "$milestone_4_files"
git diff --name-only 69b83ab..3a5e715 | sort -u > "$milestone_5_files"
test "$(comm -12 "$legacy_files" "$milestone_5_files" | wc -l | tr -d ' ')" = 3
test "$(comm -12 "$milestone_1_files" "$milestone_5_files" | wc -l | tr -d ' ')" = 7
test "$(comm -12 "$milestone_2_files" "$milestone_5_files" | wc -l | tr -d ' ')" = 7
test "$(comm -12 "$milestone_3_files" "$milestone_5_files" | wc -l | tr -d ' ')" = 7
test "$(comm -12 "$milestone_4_files" "$milestone_5_files" | wc -l | tr -d ' ')" = 3
```

Temporary accounting files may be discarded through normal temporary-file
cleanup. Do not broaden cleanup beyond those exact files.

## Verification gate

After reconstruction, run at minimum:

```bash
cd apps/showvault_app
flutter analyze
flutter test test/config/app_config_test.dart \
  test/recovery/local_support_diagnostic_service_test.dart test/app_test.dart
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

Separately approved installed proof must validate app/report hashes, report-core
digests, deep code signature, normal-build command disablement, path/credential
scans, external-vault retention, source-free rehydration, exact owned cleanup,
and disposable project cleanup. This manifest does not authorize that run.

Audit the final source and reports for unguarded command modes, non-loopback
bypass, personal/venue data, package-content reads during diagnostics, raw errors,
paths, credentials, host identity, linked destinations, unbounded metadata,
destructive uninstall behavior, external-vault deletion, cleanup without an
ownership marker, and readiness claims beyond direct evidence.

Passing this gate does not establish host reboot, rollback, clean-machine
installation, distribution signing/notarization, production-provider behavior,
Windows execution, personal-data safety, venue readiness, or Recovery Confidence.
