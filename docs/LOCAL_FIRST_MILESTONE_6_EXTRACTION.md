# Local-first milestone 6 extraction manifest

## Outcome

Milestone 6 reconstructs Windows x64 packaging, local-path safety, installed
replacement proof, manual native CI evidence, provenance-bound artifact/run
verification, and the deterministic default-branch evidence bridge.

This document authorizes local planning and host-independent verification only.
It does not authorize a push, PR operation, merge, workflow dispatch, Windows
equipment, software installation, registry mutation, external credentials,
personal data, or venue use.

Windows readiness remains unclaimed until the native and attended gates are run
on explicitly authorized controlled Windows equipment or through the separately
approved manual workflow sequence.

## Selected historical source

The contiguous span `3a5e715..2e107a8` contains 22 commits, but four are unrelated
local-first integration-planning commits and are not milestone-6 source:

- `626e88d`
- `0c174ba`
- `a1c3c83`
- `65c50be`

Use these 18 selected Windows commits:

| Commit | Historical concern |
| --- | --- |
| `58ad46a` | Windows package, installer, path safety, and installed proof |
| `5c7ade7` | Native execution gate |
| `e503ca1` | Controlled-Windows handoff |
| `6fdccca` | Manual Windows evidence workflow |
| `70fe056` | Native-run handoff |
| `ddfcaa6` | Configured legacy package-storage CI correction |
| `d5e441e` | Published evidence-gate reconciliation |
| `1dd2d23` | Isolated default-branch bridge plan |
| `a1a69eb` | Downloaded artifact verifier |
| `a66f744` | Artifact-verification handoff |
| `0644cb1` | Checksummed workflow provenance |
| `b231d4c` | Provenance dispatch boundary |
| `7592fbe` | GitHub run/workflow attestation |
| `a375e40` | Run-attestation handoff |
| `a927c20` | Deterministic bridge preparer |
| `7b6093d` | Deterministic-bridge handoff |
| `1ce2efc` | Deterministic bridge verifier |
| `2e107a8` | Bridge-verification handoff |

Their file union contains 35 paths; 21 are absent at the `3a5e715` base. Two
paths overlap the paused legacy slice, five overlap milestone 1, seven overlap
milestone 2, six overlap milestone 3, two overlap milestone 4, and six overlap
milestone 5.

Do not replay unrelated integration-plan hunks from the four excluded commits.
Reconcile shared handoff and integration documents from the reconstructed result.

## Reconstruction order

Build the milestone as six reviewable implementation/evidence commits plus
current documentation.

### 1. Windows path policy and current-user package

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/local_path_policy.dart`;
- Windows handling in
  `apps/showvault_app/lib/src/recovery/local_access_coordinator.dart`;
- matching Restore/diagnostic containment in
  `local_restore_service.dart`, `local_support_diagnostic_service.dart`, and
  `upgrade_diagnostic_harness.dart`;
- `apps/showvault_app/test/recovery/local_path_policy_test.dart` and matching
  access/scanner tests;
- `apps/showvault_app/windows/CMakeLists.txt`;
- `apps/showvault_app/windows/runner/Runner.rc` and `main.cpp`;
- `apps/showvault_app/packaging/windows/build-app.ps1`;
- `apps/showvault_app/packaging/windows/installer.iss`;
- `apps/showvault_app/test/packaging/windows_packaging_test.dart`.

Required path behavior:

- require a non-root absolute local-drive path;
- reject relative, drive-relative, root-relative, UNC/network, extended/device,
  traversal, alternate-stream, empty-segment, trailing-dot/space alias, linked,
  junction, and substituted paths;
- compare canonical paths case-insensitively with segment-bounded containment;
  and
- reject embedded drive, UNC, Unix, and `file://` paths in path-free reports.

Required package behavior:

- cleanly build `ShowVault.exe` and verify the complete Flutter deployment;
- produce a current-user Inno Setup installer, portable ZIP, closed path-free
  package manifest, observed Authenticode states, and exact `SHA256SUMS`;
- register only `showvault://` under `HKCU\Software\Classes`;
- install no Agent, service, driver, credential, or broad machine setting;
- replace only `{app}` during upgrade; and
- retain the external ShowVault Pro vault during upgrade and uninstall.

The build side requires PowerShell 7, Flutter Windows support, Visual Studio
Desktop C++, and Inno Setup 6. The installed customer target must require none of
those tools, Git, repository access, or a separate Agent.

### 2. Marker-scoped installed replacement proof

Add or reconstruct:

- `apps/showvault_app/tool/run-windows-installed-proof.ps1`;
- Windows proof support in
  `apps/showvault_app/lib/src/recovery/upgrade_diagnostic_harness.dart`.

Required behavior:

- require an absent local-drive output and an isolated controlled user;
- refuse execution if the `showvault` callback is already registered;
- create only an ownership-marked synthetic workspace;
- compile distinct before/after packages, install before, create/verify/queue,
  retry/synchronize, Restore, diagnose, and delete the synthetic source;
- replace the installed app, reopen the unchanged external vault, and prove
  source-free rehydration with the after package;
- record bounded OS/build/architecture, artifact hashes, actual Authenticode
  states, retention results, report-core digest, and explicit limitations;
- uninstall the app, remove the proof callback, ask the harness to remove its
  synthetic vault, and remove only the marker-owned workspace.

Never weaken Windows security, overwrite an existing callback, delete an
external operator vault, or broaden cleanup to neighboring files or users.

### 3. Manual native evidence workflow and provenance

Add or reconstruct `.github/workflows/windows-evidence.yml` with:

- `workflow_dispatch` only and no automatic trigger;
- repository `contents: read` only;
- exactly reviewed commit-pinned checkout, Flutter, and artifact-upload actions;
- `persist-credentials: false` on the immutable source checkout when used as the
  default-branch bridge;
- `windows-2025`, Flutter 3.44.8 x64, a 90-minute job limit, no secrets, and
  14-day synthetic artifact retention;
- native toolchain/Inno checks, analysis, complete Flutter tests including the
  NTFS-junction case, packaging, installed proof, checksum verification, callback
  and fixture cleanup; and
- a checksummed path-free provenance record based on the actual checked-out Git
  commit, manual event, run ID/attempt, job, runner OS/architecture, and artifact
  name.

The workflow must not infer provenance from a mutable branch name or upload
unchecked files. It establishes headless native evidence only, not attended
picker/Auth0, clean-customer-machine, signing trust, hardware, reboot, personal
data, or venue evidence.

### 4. Independent downloaded-artifact verification

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/windows_evidence_verifier.dart`;
- `apps/showvault_app/tool/verify_windows_evidence.dart`;
- `apps/showvault_app/test/packaging/windows_evidence_verifier_test.dart`.

Required behavior:

- require exactly the package and installed-proof artifact directories;
- refuse links, unexpected/unlisted entries, wrong names, and unsafe types;
- accept actual PowerShell LF or CRLF checksum files while verifying exact
  checksum domains;
- enforce closed package, execution metadata, resilience report, and provenance
  schemas;
- verify the internal report-core digest;
- reject paths, credentials, tokens, sensitive values, and unrestricted output;
- validate recorded Authenticode statuses as bounded evidence values; and
- emit only bounded hashes, identities, results, preservation status, and claim
  limitations.

This lower-level verifier does not establish GitHub run identity, workflow
revision, or signer trust.

### 5. GitHub run and workflow-revision attestation

Add or reconstruct:

- `apps/showvault_app/lib/src/recovery/windows_evidence_run_verifier.dart`;
- `apps/showvault_app/tool/verify_windows_run.dart`;
- `apps/showvault_app/test/packaging/windows_evidence_run_verifier_test.dart`.

Required behavior:

- accept an explicit workflow run ID and absent output directory;
- require the expected completed successful manual workflow/run/job identity;
- fetch the workflow at the run's exact head SHA;
- attest the manual/read-only/no-secret boundary and one immutable source pin;
- download only the named artifact and invoke the independent artifact verifier;
- require artifact provenance to match the real run ID, attempt, source pin, job,
  runner, and artifact name; and
- perform no dispatch, branch, PR, release, or repository mutation.

A failed verification preserves only the bounded downloaded directory needed for
diagnosis and makes no readiness claim.

### 6. Deterministic bridge preparation and verification

Add or reconstruct:

- `windows_evidence_bridge_preparer.dart` and its tool/test;
- `windows_evidence_bridge_verifier.dart` and its tool/test.

The preparer must accept only:

- an explicitly approved lowercase full source SHA;
- the audited regular product workflow;
- the exact approved action pins and manual/read-only boundary; and
- an existing regular output parent with an absent file named
  `windows-evidence.yml`.

It injects exactly one immutable checkout `ref`, disables persisted credentials,
rereads the created file, and emits a bounded digest. It refuses overwrite,
linked input/output, unsafe filenames, mutable refs, and workflow-policy drift.

The verifier independently regenerates the expected bridge in memory and
requires the candidate's exact filename and byte-for-byte equality, rejecting
content, source pin, line ending, filename, and link substitutions. Preparation
and verification perform no GitHub operation.

### 7. Current execution and integration runbooks

Reconcile only Windows-relevant content in:

- `docs/WINDOWS_PACKAGING_AND_EXECUTION.md`;
- `docs/WINDOWS_EVIDENCE_INTEGRATION_PLAN.md`;
- `docs/PROTOTYPE_READINESS.md`;
- `README.md` and `apps/showvault_app/README.md`.

Regenerate `CHAT_CONTINUATION_README.md` after verification. Keep the one-file
default-branch bridge separate from accumulated product integration. PR #25 is a
comparison view, not a Windows-only merge unit. A source push, PR #26 update,
ready/merge action, manual dispatch, and controlled Windows use each require the
explicit authorization documented in the active handoff.

## Exact CI correction

Commit `ddfcaa6` changes
`services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageWriter.cs`.
When a legacy `PackageDirectory` is configured, the writer must not construct
`LocalVaultLayout` and therefore must not resolve an unavailable default
Documents vault. Default-vault mode still constructs and fails closed through
the normal local-vault path.

This corrects the file ownership recorded in earlier planning: the behavior does
not belong to `AgentQueueStore`.

## Complete file accounting

The 18 selected commits touch a union of 35 paths:

- 1 manual workflow file;
- 27 desktop/native/package/proof/verifier source, tool, and test files;
- 1 Agent compatibility correction; and
- 6 repository/runbook files.

Twenty-one of the 35 paths are absent at the `3a5e715` base.

Reproduce the selected union from the repository root with Bash:

```bash
selected=(
  58ad46a 5c7ade7 e503ca1 6fdccca 70fe056 ddfcaa6
  d5e441e 1dd2d23 a1a69eb a66f744 0644cb1 b231d4c
  7592fbe a375e40 a927c20 7b6093d 1ce2efc 2e107a8
)
selected_files="$(mktemp)"
for commit in "${selected[@]}"; do
  git diff-tree --no-commit-id --name-only -r "$commit"
done | sort -u > "$selected_files"
test "${#selected[@]}" = 18
test "$(wc -l < "$selected_files" | tr -d ' ')" = 35

added=0
while IFS= read -r file_path; do
  if ! git cat-file -e "3a5e715:$file_path" 2>/dev/null; then
    added=$((added + 1))
  fi
done < "$selected_files"
test "$added" = 21
```

Cross-milestone overlap counts are 2 legacy, 5 milestone 1, 7 milestone 2, 6
milestone 3, 2 milestone 4, and 6 milestone 5 paths. The four excluded
integration-planning commits must not add paths or hunks to this milestone.

Temporary accounting files may be discarded through normal temporary-file
cleanup. Do not broaden cleanup beyond those exact files.

## Host-independent verification gate

Before any external action, run on the current local host:

```bash
cd apps/showvault_app
flutter analyze
flutter test test/packaging/windows_packaging_test.dart \
  test/packaging/windows_evidence_verifier_test.dart \
  test/packaging/windows_evidence_run_verifier_test.dart \
  test/packaging/windows_evidence_bridge_preparer_test.dart \
  test/packaging/windows_evidence_bridge_verifier_test.dart \
  test/recovery/local_path_policy_test.dart \
  test/recovery/local_access_coordinator_test.dart
flutter test

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

Also parse/inspect the workflow locally and perform a prepare-to-verify dry run
only into newly created local temporary paths. Do not invoke the run verifier
without an explicitly approved completed run and absent download directory.

## Native and attended gates

On separately authorized controlled Windows 10/11 x64 equipment:

- validate PowerShell and Inno syntax/build in their native engines;
- run the complete Flutter suite including the NTFS-junction test;
- build into an absent local-drive directory and verify complete deployment,
  package manifest, ZIP/installer hashes, and actual Authenticode states;
- prove current-user callback registration, launch, upgrade replacement,
  uninstall retention, source-free rehydration, Restore, diagnostics, privacy,
  and exact cleanup;
- execute attended exact-catalog Scan, native pickers, and interactive Auth0
  callback behavior separately from headless CI; and
- record exact OS build, architecture, artifact hashes, results, and limitations.

Audit all source, workflows, packages, reports, and downloaded evidence for
secrets, credentials, exact paths, host identity, mutable source refs, unpinned
actions, write permissions, unexpected artifacts, false Authenticode trust,
Agent/service installation, machine-wide registry changes, vault deletion,
unsafe junction/UNC/device handling, broad cleanup, and claims beyond evidence.

Passing headless CI does not establish attended UX, commercial-session expiry,
provider quota/outage behavior, distribution signing, hardware/driver support,
reboot persistence, personal-data safety, clean-machine support range, venue
readiness, dependency closure, or Recovery Confidence.
