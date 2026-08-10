# Local-first milestone 1 extraction manifest

## Outcome

Milestone 1 reconstructs the venue-neutral direct desktop Scan and guarded
personal-beta shell on the then-current integrated `main`. It does not merge the
accumulated PR #25 stack and grants no permission to push, open or update a PR,
merge, dispatch a workflow, or use external equipment.

The customer outcome is:

**Install → Scan this computer → Sign in for cloud service**

Scan checks only exact approved catalog candidates. It sends only opaque
candidate keys to a manager-authorized, tenant-scoped API. It does not install or
enroll an Agent, enumerate unrelated applications, read candidate contents,
collect machine identity, inspect networks, or claim backup/recovery readiness.

## Historical source boundary

The source reference is the nine-commit range `310190c..ce5be25`:

| Commit | Historical concern |
| --- | --- |
| `b47a2ae` | Venue-neutral prototype gates |
| `6108b0d` | Personal-test macOS packaging |
| `a4d4785` | Packaging handoff |
| `7e04e74` | Catalog-only personal-computer scan through legacy Agent compatibility |
| `d2c5d47` | Scan-beta handoff |
| `3ed4bdc` | Direct desktop Scan without Agent enrollment |
| `90e8d22` | Direct-scan handoff |
| `eea1d45` | Restored navigation and guarded no-login beta |
| `ce5be25` | Accounts-plan handoff |

These commits are evidence and source material, not a cherry-pick sequence. They
were authored on top of the paused 247-commit catalog/Agent slice.

The range has 41 net-changed files: 23 also changed in the paused legacy slice
and 18 are new to this range. The commit union contains 43 files; these two
navigation files are net-zero and must not be included merely because they
appear in intermediate commits:

- `apps/showvault_app/lib/src/navigation/app_router.dart`
- `apps/showvault_app/lib/src/navigation/app_shell.dart`

The full-sidebar product decision is preserved. Any later navigation change
requires a separate Product Owner decision.

## Reconstruction order

Build the milestone as five reviewable commits. A commit may be split further,
but these concerns must not be collapsed across their stated security boundary.

### 1. Tenant-scoped direct-scan persistence and API

Add or reconstruct:

- `services/platform/src/ShowVault.Platform/Agents/DesktopCatalogScan.cs`
- `services/platform/src/ShowVault.Platform/Agents/DesktopCatalogScanCandidate.cs`
- `services/api/src/ShowVault.Api/Contracts/RecoveryCandidateContracts.cs`
- `services/api/src/ShowVault.Api/Data/PlatformDbContext.cs`
- `services/api/src/ShowVault.Api/Endpoints/RecoveryCandidateEndpoints.cs`
- `services/api/tests/ShowVault.Api.Tests/AgentEnrollmentTests.cs` — direct-scan
  cases only in this commit
- the two desktop-scan migrations and their designer files
- `services/api/src/ShowVault.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs`
  regenerated from the composed model

Required behavior:

- independently allowlist every opaque key on the server;
- require manager/administrator/owner access to the exact organization/venue;
- reject unknown keys, paths, oversized input, and cross-tenant access;
- store a scan header even for an empty scan so newer absence supersedes stale
  detections;
- return only the newest scan and label direct results as detected, never
  approved, protected, verified, or recoverable; and
- keep candidate key, product, type, and evidence bounded and path-free.

Do not hand-edit or transplant the EF snapshot independently. Regenerate it and
require the pending-model check to report no changes.

### 2. Exact-candidate desktop scanner and signed-out shell

Add or reconstruct:

- `apps/showvault_app/lib/src/scanning/local_catalog_scanner.dart`
- `apps/showvault_app/test/scanning/local_catalog_scanner_test.dart`
- `apps/showvault_app/lib/src/api/showvault_api.dart`
- `apps/showvault_app/test/api/showvault_api_test.dart`
- only the milestone-1 Scan/signed-out-shell portions of
  `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart`
- the matching milestone-1 portions of `apps/showvault_app/test/app_test.dart`

Required behavior:

- evaluate exact catalog-defined macOS and Windows candidates only;
- use an explicit synthetic-home seam in tests and suppress real application
  candidates in synthetic mode;
- keep exact paths transient in application memory;
- submit only opaque keys and keep local findings if cloud submission fails;
- keep Scan available while signed out or offline; and
- prevent direct detections from entering legacy Agent approval, validation,
  backup, or recovery controls.

Do not import later Save, vault, synchronization, Restore, or diagnostic UI in
this commit. Those belong to milestones 2 and 3.

### 3. Guarded personal-beta authentication and packaging

Add or reconstruct:

- `apps/showvault_app/lib/src/config/app_config.dart`
- `apps/showvault_app/lib/src/auth/auth_service.dart`
- the authentication portions of
  `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart`
- `services/api/src/ShowVault.Api/Security/PersonalBetaAuthenticationHandler.cs`
- `services/api/tests/ShowVault.Api.Tests/PersonalBetaAuthenticationTests.cs`
- only the personal-beta authentication portion of
  `services/api/src/ShowVault.Api/Program.cs`
- `apps/showvault_app/macos/Runner/Configs/AppInfo.xcconfig`
- `apps/showvault_app/packaging/macos/build-app.sh`
- the matching operator instructions in `apps/showvault_app/README.md`

The bypass must require all of the following simultaneously:

- an explicit compile-time application flag;
- a loopback HTTP API origin;
- server Development environment;
- an explicit server flag and an existing bounded test identity; and
- a loopback client request.

Normal builds continue to use Auth0. macOS and Windows sessions stay in
application memory only; ShowVault must not read or write the user's personal
login Keychain. The personal-test package remains ad hoc and unnotarized and
must not be described as a distribution artifact.

### 4. Bounded Agent protocol compatibility

Reconstruct only if protocol 1.21 compatibility remains required on the
then-current base:

- `services/contracts/src/ShowVault.AgentContracts/AgentProtocol.cs`
- the `CollectCatalogApplications` portions of
  `services/agent/src/ShowVault.Agent/Execution/AgentCommandExecutor.cs`
- `services/agent/src/ShowVault.Agent/Plugins/LocalApplicationDetectionRegistry.cs`
- `services/agent/src/ShowVault.Agent/Plugins/SystemInventoryPlugin.cs`
- the catalog privacy cases in
  `services/agent/tests/ShowVault.Agent.Tests/AgentCommandExecutorTests.cs`
- `services/agent/tests/ShowVault.Agent.Tests/LocalRecoveryCandidateDiscoveryTests.cs`
- `services/api/src/ShowVault.Api/Endpoints/RecoveryWorkflowEndpoints.cs`
- only the Agent inventory-command case in
  `services/api/tests/ShowVault.Api.Tests/AgentEnrollmentTests.cs`

This command must remain catalog-only, path-free off the Agent, and distinct
from full system inventory. Retain the corrected nested Resolume application
candidates. Do not expose Agent installation, enrollment, inventory, service
management, or this compatibility endpoint in the customer desktop flow.

### 5. Current product and readiness documentation

Reconcile rather than blindly replay:

- `README.md`
- `docs/PRODUCT.md`
- `docs/PROTOTYPE_READINESS.md`
- `docs/AUTOMATIC_DISCOVERY.md`
- `docs/PROTOTYPE_RUNBOOK.md`
- `docs/ROADMAP.md`
- `docs/SYSTEM_INVENTORY_PLUGIN.md`
- `docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md`

Regenerate `CHAT_CONTINUATION_README.md` only after the reconstructed code and
verification results are known. Historical personal-Mac evidence may be kept as
bounded evidence, but it must not be presented as evidence for a newly
reconstructed branch unless rerun and verified there.

## File-accounting checks

From the repository root, the historical range must satisfy:

```bash
test "$(git rev-list --count 310190c..ce5be25)" = 9
test "$(git diff --name-only 310190c..ce5be25 | sort -u | wc -l | tr -d ' ')" = 41
```

To reproduce the 23 overlap and 18 range-only paths:

```bash
legacy_files="$(mktemp)"
milestone_files="$(mktemp)"
git diff --name-only 254cbbf..310190c | sort -u > "$legacy_files"
git diff --name-only 310190c..ce5be25 | sort -u > "$milestone_files"
test "$(comm -12 "$legacy_files" "$milestone_files" | wc -l | tr -d ' ')" = 23
test "$(comm -13 "$legacy_files" "$milestone_files" | wc -l | tr -d ' ')" = 18
```

Temporary files created only for this read-only audit may be discarded through
the operator's normal temporary-file cleanup. Do not place them in the
repository or broaden cleanup beyond those exact files.

## Verification gate

After reconstruction, run at minimum:

```bash
cd apps/showvault_app
flutter analyze
flutter test test/scanning/local_catalog_scanner_test.dart \
  test/api/showvault_api_test.dart test/app_test.dart
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

Also inspect the final diff for exact-path leakage, unrestricted application or
filesystem enumeration, machine/network inventory, personal-Keychain calls,
unguarded bypass configuration, tenant authorization gaps, direct detections in
legacy recovery controls, and stale claims copied from handoff documents.

No passing test authorizes a push, PR operation, merge, workflow dispatch,
external equipment, personal data, or venue use.
