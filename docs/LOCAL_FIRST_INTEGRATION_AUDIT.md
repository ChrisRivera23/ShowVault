# Local-first integration overlap audit

## Purpose

This audit makes the post-PR #24 product-integration plan executable without
granting permission to push, retarget, merge, dispatch a workflow, or use
external equipment. It classifies the 29 files changed by both the paused
247-commit catalog/Agent slice and the following 40 product-directed commits.

The audit is not a complete extraction manifest. Files introduced only by the
40-commit product tail do not appear here and must still be reviewed in their
milestone. Repository code, tests, migrations, and
`LOCAL_FIRST_PRODUCT_BIBLE.md` remain authoritative.

## Reproducible boundary

The audited published boundaries are:

- PR #24 head: `254cbbf9dca616f69f494934497f58c71095100e`
- end of paused legacy slice: `310190c`
- published PR #25 head: `ddfcaa6af7ccd03a1e7ae8d6de29f0865a81e97b`

From the repository root, reproduce the overlap with:

```bash
comm -12 \
  <(git diff --name-only 254cbbf..310190c | sort -u) \
  <(git diff --name-only 310190c..ddfcaa6 | sort -u)
```

The command must return exactly 29 paths. Verify the commit decomposition with:

```bash
git rev-list --count 254cbbf..310190c
git rev-list --count 310190c..ddfcaa6
```

The expected counts are 247 and 40. These checks use local Git objects only.

## Disposition rules

- **Carry** means reconstruct the final behavior in the named milestone and
  review it against the then-current base.
- **Split** means one historical file contains concerns belonging to multiple
  milestones; do not cherry-pick or apply its full tail diff.
- **Regenerate** means recreate the derived or handoff artifact from the
  integrated source rather than replaying history.
- **Compatibility** means retain a bounded legacy Agent behavior only where the
  final product still depends on it. It must not appear in customer onboarding.

## Repository and documentation files

| File | Disposition | Required handling |
| --- | --- | --- |
| `.gitignore` | Carry | Add only exclusions required by reconstructed milestones, including local configuration artifacts. |
| `CHAT_CONTINUATION_README.md` | Regenerate | Refresh from the integrated result; do not import historical evidence or stale branch/PR state as product code. |
| `README.md` | Split | Rewrite the architecture summary from the product bible. Remove or clearly label legacy Agent claims that no longer describe customer onboarding. |
| `docs/AUTOMATIC_DISCOVERY.md` | Split | Preserve bounded discovery research separately from direct desktop Scan. Do not convert paused enumeration work into customer behavior. |
| `docs/PRODUCT.md` | Carry | Retain venue-neutral scope and the explicit controlled-equipment boundary. |
| `docs/PROTOTYPE_RUNBOOK.md` | Split | Keep legacy authenticated Agent proof as historical compatibility evidence; point current readiness work to the local-first runbooks. |
| `docs/SYSTEM_INVENTORY_PLUGIN.md` | Split | Describe `CollectCatalogApplications` only as compatibility infrastructure and direct Scan as the customer path. |

## Desktop files

| File | Disposition | Required handling |
| --- | --- | --- |
| `apps/showvault_app/lib/src/api/showvault_api.dart` | Carry in milestone 1 | Add path-free direct-scan submission and returned candidate keys without weakening tenant authorization. |
| `apps/showvault_app/lib/src/auth/auth_service.dart` | Carry in milestone 1 | Preserve in-memory macOS/Windows sessions and the compile-time, loopback-only personal-beta bypass. Never restore personal-Keychain use. |
| `apps/showvault_app/lib/src/dashboard/dashboard_screen.dart` | Split across milestones 1-3 | Reconstruct the signed-out local shell and direct Scan first, then Save/vault rehydration, then hosted sync/Restore/diagnostics. Do not restore customer Agent enrollment or recovery controls. |
| `apps/showvault_app/test/api/showvault_api_test.dart` | Carry with milestone 1 | Prove the direct-scan request contains only opaque candidate keys and no path. |

## Legacy Agent compatibility files

| File | Disposition | Required handling |
| --- | --- | --- |
| `services/agent/src/ShowVault.Agent/AgentOptions.cs` | Compatibility in milestone 2 | Carry configurable vault layout only for retained Agent package compatibility. |
| `services/agent/src/ShowVault.Agent/AgentWorker.cs` | Compatibility in milestone 2 | Initialize the local vault only under the final fail-closed configuration rules. |
| `services/agent/src/ShowVault.Agent/Execution/AgentCommandExecutor.cs` | Split | Keep bounded catalog collection only if protocol 1.21 compatibility remains required; add verified-only upload queuing with the local-vault milestone. |
| `services/agent/src/ShowVault.Agent/Plugins/LocalApplicationDetectionRegistry.cs` | Compatibility in milestone 1 | Retain only exact approved catalog candidates and the corrected nested Resolume application paths. Do not broaden enumeration. |
| `services/agent/src/ShowVault.Agent/Plugins/SystemInventoryPlugin.cs` | Compatibility in milestone 1 | Keep the narrow catalog-only result separate from full system inventory. It is not the customer Scan implementation. |
| `services/agent/src/ShowVault.Agent/Program.cs` | Compatibility in milestone 2 | Register and validate the local vault without making customer installation depend on an Agent service. |
| `services/agent/src/ShowVault.Agent/Queue/AgentQueueStore.cs` | Compatibility in milestone 2 | Preserve verified-only upload jobs and the published `ddfcaa6` correction: configured legacy package storage must not force unavailable default-vault resolution. |
| `services/agent/src/ShowVault.Agent/appsettings.json` | Compatibility in milestone 2 | Keep nullable vault configuration and no embedded venue, credential, or source data. |
| `services/agent/tests/ShowVault.Agent.Tests/AgentCommandExecutorTests.cs` | Split | Separate catalog metadata/privacy coverage from verified-only queue coverage. |
| `services/agent/tests/ShowVault.Agent.Tests/LocalRecoveryCandidateDiscoveryTests.cs` | Compatibility in milestone 1 | Retain exact bounded macOS/Windows candidate-path tests, including the nested Resolume layout. |
| `services/contracts/src/ShowVault.AgentContracts/AgentProtocol.cs` | Compatibility in milestone 1 | Advance the protocol only with the matching bounded command implementation and tests; never expose it as a customer setup requirement. |

## API and persistence files

| File | Disposition | Required handling |
| --- | --- | --- |
| `services/api/src/ShowVault.Api/Contracts/RecoveryCandidateContracts.cs` | Carry in milestone 1 | Add the closed direct-scan request/response and path-free candidate-key fields. |
| `services/api/src/ShowVault.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs` | Regenerate | Generate from the composed desktop-scan migrations and verify no pending model changes. Never hand-merge the snapshot as an isolated source change. |
| `services/api/src/ShowVault.Api/Data/PlatformDbContext.cs` | Carry in milestone 1 | Add tenant-scoped desktop scan headers/candidates with bounded fields and cascading venue ownership. |
| `services/api/src/ShowVault.Api/Endpoints/RecoveryCandidateEndpoints.cs` | Carry in milestone 1 | Independently allowlist every key, require manager-level venue access, cap input, persist empty scans, and return only the newest direct scan. |
| `services/api/src/ShowVault.Api/Endpoints/RecoveryWorkflowEndpoints.cs` | Compatibility in milestone 1 | Add the Agent catalog command endpoint only if protocol 1.21 compatibility is retained; keep it out of customer desktop controls. |
| `services/api/src/ShowVault.Api/Program.cs` | Split across milestones 1 and 4 | Add guarded Development personal-beta authentication with direct Scan, then add hosted-storage startup, migrations, and readiness endpoints with deployable storage. Production must remain fail-closed. |
| `services/api/tests/ShowVault.Api.Tests/AgentEnrollmentTests.cs` | Split | Separate direct-scan authorization/allowlist/privacy tests from legacy Agent inventory-command compatibility tests. |

## Extraction gates

`LOCAL_FIRST_MILESTONE_1_EXTRACTION.md` is the complete source/file/test
manifest for the first reconstruction milestone. Use it together with the
overlap dispositions above; neither document grants external-action authority.

`LOCAL_FIRST_MILESTONE_2_EXTRACTION.md` fixes the second milestone to the local
vault, offline Save/Verify, native access, and restart-rehydration boundary. It
also requires the later `ddfcaa6` compatibility correction during reconstruction.

Before a milestone is proposed for integration:

1. Start from the then-current integrated `main`, never from PR #25.
2. Review every overlap row assigned to that milestone and every newly added
   file in the corresponding historical range.
3. Confirm the customer UI contains no Agent installation, enrollment code,
   service setup, broad inventory, or personal-Keychain path.
4. Run focused tests, the complete relevant Flutter/.NET suites, EF migration
   and pending-model checks where applicable, privacy and tenant-isolation
   checks, and `git diff --check`.
5. Compare the reconstructed behavior with the current branch at the relevant
   milestone boundary. Differences must be deliberate and documented.
6. Keep synthetic fixtures and local execution as the default. Remote branches,
   PRs, workflow dispatch, and controlled equipment remain separately
   authorized actions.
