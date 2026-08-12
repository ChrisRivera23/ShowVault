# PR #13 bounded local implementation evidence

## Outcome

The approved PR #13 system-inventory disposition is implemented locally as
legacy Agent compatibility infrastructure. It remains separate from the
customer desktop **Scan this computer** path and adds no Agent onboarding,
network discovery, catalog expansion, equipment access, or venue behavior.

No GitHub state was changed.

## Exact source

- Branch: `codex/pr13-bounded-system-inventory`
- Worktree: `/private/tmp/showvault-pr13-implementation.dEnJJa`
- Base/current remote `main`:
  `f37d22f69d6b5ae9427690fd8674be20c224aff0`
- Implementation commit:
  `ee47b919d076e684f7d925d11f883016028020a8`
- Implementation tree:
  `5b52b40eedf747c2617583b2e6c7a953228b5f1b`
- Scope: 9 files, `+576/-4`
- Binary-diff SHA-256:
  `a2f5b0aa104ba1f4dae32e269be495ca6cbb4ab25bba98dc1c2bc1e007b21a40`
- Path-list SHA-256:
  `269050b08988ed8b1a6beef0fda955b7b48fe8f73d0915ae67d97edf9ea943ef`

The implementation commit is directly parented by the exact reviewed `main`.
It does not cherry-pick the obsolete/conflicting historical README change.

## Implemented boundary

- Protocol 1.1 adds only `CollectSystemInventory`.
- `SystemInventory` and `ReadSystemInformation` remain explicit, separate
  capability and permission values.
- The plugin is registered only in the legacy Agent process.
- A production source reads bounded platform facts through `Environment`,
  `RuntimeInformation`, and `DriveInfo` without shell commands, subprocesses,
  network access, registry traversal, credentials, or file-content reads.
- The plugin enforces limits for machine name, OS/architecture strings,
  processor count, volume name/type, capacities, and at most 64 volumes.
- Unready or unreadable volume capacities remain null.
- Cancellation is checked before host access and around each enumerator step.
- Tests inject synthetic facts and do not inspect the developer or CI host.
- The complete inventory is stored in the Agent's local durable result before
  a completion event is queued.
- Completion uses the current `Running → Completed` transition invariant.
- Completion exposes only plugin ID, bounded OS description/architecture,
  processor count, and volume count. Machine and volume identifiers remain
  absent from events, errors, and logs.
- Re-execution after completion is idempotent and emits no duplicate event.
- Source failures produce only the closed `command-not-executable` category.
- Cancellation emits no success or failure event and leaves the running command
  available for the existing restart/resume path.

## Changed paths

- `docs/SYSTEM_INVENTORY_PLUGIN.md`
- `services/agent/src/ShowVault.Agent/Execution/AgentCommandExecutor.cs`
- `services/agent/src/ShowVault.Agent/Plugins/PluginContracts.cs`
- `services/agent/src/ShowVault.Agent/Plugins/SystemInventoryPlugin.cs`
- `services/agent/src/ShowVault.Agent/Program.cs`
- `services/agent/tests/ShowVault.Agent.Tests/AgentCommandExecutorTests.cs`
- `services/agent/tests/ShowVault.Agent.Tests/SystemInventoryPluginTests.cs`
- `services/contracts/src/ShowVault.AgentContracts/AgentProtocol.cs`
- `services/contracts/tests/ShowVault.AgentContracts.Tests/AgentCommandEnvelopeTests.cs`

There are no Flutter, API endpoint, persistence-schema, migration, workflow,
packaging, network-discovery, catalog, or root README changes.

## Validation

Executed from the exact implementation worktree before the commit, with no
source changes afterward:

- `dotnet format` contracts test project, verify-only: passed.
- `dotnet format` Agent test project, verify-only: passed.
- Contracts: 22 passed, 0 failed, 0 skipped.
- Platform: 15 passed, 0 failed, 0 skipped.
- Agent: 104 passed, 0 failed, 0 skipped.
- API: 11 passed, 0 failed, 0 skipped.
- `git diff HEAD --check`: passed before commit.
- `git show --check ee47b91`: passed after commit.
- Worktree after implementation commit: clean.

The tests include synthetic maximum-volume behavior, host/volume field bounds,
invalid capacity combinations, cancellation before and during enumeration,
protocol support, durable local storage, `Running → Completed`, idempotency,
outbound identifier exclusion, and bounded failure output.

## Evidence limits

This is local .NET and source evidence only. No native application, installer,
signing, authentication callback, hosted workflow, real network, equipment,
customer data, or venue test was run. None is required for this legacy-Agent
compatibility slice, and none may be inferred from these results.

## Publication stop

The implementation and this evidence remain local. Before any push or PR #13
mutation:

1. Reconfirm live `main`, PR #13 base/head/body, reviews, checks, permissions,
   rulesets, and merge policy.
2. Prove the candidate is still directly based on the exact intended `main`.
3. Recompute and compare the commit, tree, scope, diff, and path-list hashes.
4. Prepare an exact source-branch replacement/retarget and PR-description plan.
5. Stop for explicit authorization immediately before each remote mutation.

Do not begin PR #14 while PR #13 remains unpublished or its disposition has not
been reconciled with the live dependency topology.
