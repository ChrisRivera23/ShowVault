# PR #15 bounded local implementation evidence

## Outcome

The approved narrowed Resolume slice is implemented locally as legacy Agent
**Assisted** compatibility. It accepts only explicitly configured portable
bundle roots, keeps detailed discovery results local, and fails closed when the
source identity, content, or exact topology is not stable through packaging.

No GitHub state changed, no workflow was dispatched, and no equipment was used.

## Exact source

- Branch: `codex/pr15-bounded-resolume`
- Worktree: `/private/tmp/showvault-pr15-repair.R0vNnG`
- Base/current remote `main`:
  `503845ff1f52101d45a238fd123138103275a063`
- Implementation commit:
  `3fc8155f1f9509ca68fd2c7a015988271335e8a0`
- Implementation tree:
  `7148339788cac0099768dac839d17a9677aace7f`
- Scope: 10 files, `+923/-9`
- Binary-diff SHA-256:
  `25fc18cf2c2602ea5d64d419ec3528ca2a9b5aa9fe09fc2ad8debd50a147ef92`
- Path-list SHA-256:
  `c53fd1dc85d2a98390d59470d63816d16ebb75ae4e5621bf7235c65a401d7a3a`

The implementation commit is directly parented by exact current `main`. It
does not import the historical PR #15 README, integration catalog, product, or
roadmap changes, and it has no dependency on the dropped PR #14 network slice.

## Implemented boundary

- `showvault.resolume` is registered only in the legacy Agent process.
- `ResolumeDiscoveryRoots` defaults empty and permits at most 32 unique,
  absolute, exact bundle roots. Parent, sibling, and descendant authority is
  not inferred.
- A bundle requires a regular root-level `.avc` composition and permits at
  most 128 regular files. Excess files fail rather than return a truncated
  inventory.
- Root, directory, and file handles are opened without following filesystem
  links or Windows reparse points. File identities and handles remain retained
  for validation and package copying.
- Discovery checks exact names, identities, sizes, hashes, and topology before
  returning a local result.
- Packaging recaptures the exact tree, requires a byte-for-byte manifest file
  set, copies from retained file handles, and performs a final identity,
  topology, size, and hash validation before publishing the manifest/package.
- A late extra file or same-content file replacement fails and leaves no
  published package.
- Existing command outcomes remain path-free; full paths, filenames,
  timestamps, sizes, and hashes remain in Agent-local storage.
- Documentation labels this Assisted compatibility and states that ShowVault
  does not yet parse `.avc` dependencies or prove a complete Resolume recovery.

## Changed paths

- `docs/RESOLUME_PLUGIN.md`
- `services/agent/src/ShowVault.Agent/AgentOptions.cs`
- `services/agent/src/ShowVault.Agent/Plugins/ResolumeDiscoveryPlugin.cs`
- `services/agent/src/ShowVault.Agent/Program.cs`
- `services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageWriter.cs`
- `services/agent/src/ShowVault.Agent/Recovery/StableSourceSnapshot.cs`
- `services/agent/src/ShowVault.Agent/appsettings.json`
- `services/agent/tests/ShowVault.Agent.Tests/AgentCommandExecutorTests.cs`
- `services/agent/tests/ShowVault.Agent.Tests/RecoveryPackageWriterTests.cs`
- `services/agent/tests/ShowVault.Agent.Tests/ResolumeDiscoveryPluginTests.cs`

There are no Flutter, API endpoint, persistence-schema, migration, workflow,
network-discovery, catalog, product, roadmap, or root README changes.

## Validation

Executed from the isolated exact-main worktree before the implementation
commit, with no source changes afterward:

- Focused Resolume and late-source tests: 9 passed before the additional
  same-content replacement proof was added.
- Agent: 114 passed, 0 failed, 0 skipped after all implementation tests.
- Contracts: 22 passed, 0 failed, 0 skipped.
- Platform: 15 passed, 0 failed, 0 skipped.
- API: 11 passed, 0 failed, 0 skipped.
- Flutter dependency resolution: passed without tracked changes.
- Flutter analysis: no issues.
- Flutter: 16 passed.
- Agent Release build: succeeded with 0 warnings and 0 errors.
- Agent source and test `dotnet format --verify-no-changes`: passed.
- Pre-commit staged diff check and post-commit `git show --check`: passed.

Regression coverage includes exact-root enforcement, unrelated-root rejection,
linked-root and linked-content rejection, the root-level composition gate,
oversized-bundle failure, cancellation, deterministic hashing, local-to-package
execution, path-free outcomes, late unexpected files, and late file identity
replacement.

## Evidence limits

This is local macOS .NET/Flutter and synthetic filesystem evidence. It is not a
Windows-native proof, a real Resolume export validation, a dependency-complete
composition proof, an application reopen/output test, an installer/signing
test, or an equipment/venue test. None may be inferred from these results.

## Publication stop

The implementation and this evidence remain local. Before any push or PR #15
mutation:

1. Reconfirm live `main` and PR #15 base/head/title/body, checks, review
   surfaces, permissions, rulesets, protection, and merge policy.
2. Prove this candidate is still directly based on the intended exact `main`.
3. Recompute candidate head/tree, commit scope, diff, and path-list hashes.
4. Prepare an exact old-head-leased source replacement, retarget, and corrected
   title/body proposal for the narrowed compatibility boundary.
5. Stop for explicit authorization immediately before remote mutation.

Do not begin PR #16, dispatch workflows, or use equipment during that preflight.
