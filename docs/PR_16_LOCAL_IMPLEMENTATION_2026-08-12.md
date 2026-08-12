# PR #16 bounded local implementation evidence

## Result

The approved narrowed Resolume user-data compatibility slice is implemented
locally on `codex/pr16-bounded-resolume-user-data`, directly from exact `main`
`7f4dd05b2c85c7ff656425c11cd6d213460657ec`.

Implementation commit:

- commit: `d01a252bd9959011204d417323e7708530b21943`;
- tree: `c9720ed5e93d22b737ff8c8f693caa9798871eb5`;
- scope: 10 files, `+934/-37`;
- binary-diff SHA-256:
  `be08e0751a5e6229e5ff9c665479b84ca70e96b518d24c371dd6a1249dbba09a`;
- path-list SHA-256:
  `139fae30a46f018e32f6a6fcb706bd4b87de3d0d3a107f93fb9f6e30c7ee3008`.

No GitHub state, workflow, external host, customer data, venue network, or
equipment was accessed or changed.

## Implemented boundary

The slice adds distinct versioned profile
`showvault.resolume-user-data`/`1.0.0`. It remains legacy-Agent Assisted
compatibility and does not replace or alter the direct native desktop **Scan
this computer** product direction.

`ResolumeUserDataRoots` is disabled by default and accepts at most 32 unique
absolute exact roots. Startup validation rejects overlap with portable-bundle
`ResolumeDiscoveryRoots`, and the plugin repeats the ambiguity check at use.

The profile selects only exact English, case-sensitive top-level categories:

- `Compositions`;
- `Fixture Library`;
- `Preferences`;
- `Presets`; and
- `Shortcuts`.

`Extra Effects` and `Recorded` remain excluded. Unknown siblings are enumerated
only as root names required to select categories; they are never opened,
inventoried, packaged, logged, or emitted in outcomes. Empty selected
directories are rejected so every retained selected directory is represented
by a manifest file ancestry.

Capture retains no-follow root, directory, and regular-file identities. It
revalidates selected topology, identities, sizes, and hashes. Packaging
recaptures the same profile, requires the exact manifest file set, copies from
held file handles, and repeats the stable validation before publication.

Profile-specific limits are:

- 2,048 files;
- 256 directories;
- 1,024 characters per relative path;
- 16 MiB per file;
- 128 MiB total;
- 30 seconds for discovery; and
- two minutes for package creation.

Cancellation and any limit, link, non-regular entry, identity, topology, size,
or content failure return no partial inventory and publish no partial package.
Detailed paths, names, timestamps, sizes, and hashes remain in protected local
records; completion outcomes remain path-free.

Restore behavior was not broadened. The package can use only the existing
attended empty-target restore flow and is not written into a live Resolume
Documents tree.

## Changed paths

- `docs/RESOLUME_PLUGIN.md`
- `services/agent/src/ShowVault.Agent/AgentOptions.cs`
- `services/agent/src/ShowVault.Agent/Plugins/ResolumeUserDataDiscoveryPlugin.cs`
- `services/agent/src/ShowVault.Agent/Program.cs`
- `services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageWriter.cs`
- `services/agent/src/ShowVault.Agent/Recovery/StableSourceSnapshot.cs`
- `services/agent/src/ShowVault.Agent/appsettings.json`
- `services/agent/tests/ShowVault.Agent.Tests/AgentCommandExecutorTests.cs`
- `services/agent/tests/ShowVault.Agent.Tests/ResolumeUserDataDiscoveryPluginTests.cs`
- `services/agent/tests/ShowVault.Agent.Tests/ResolumeUserDataRecoveryPackageTests.cs`

There are no root README, Flutter source, API endpoint, persistence schema,
migration, workflow, network-discovery, catalog, roadmap, or customer
onboarding changes.

## Validation

Executed from the isolated exact-main worktree:

- focused portable and user-data Resolume tests: 25 passed after the final
  empty-topology proofs;
- Agent: 133 passed, 0 failed, 0 skipped;
- contracts: 22 passed, 0 failed, 0 skipped;
- platform: 15 passed, 0 failed, 0 skipped;
- API: 11 passed, 0 failed, 0 skipped;
- Entity Framework pending-model gate: no model changes;
- Flutter dependency resolution: passed without tracked changes;
- Flutter analysis: no issues;
- Flutter: 16 passed;
- Agent Release build: 0 warnings and 0 errors;
- Agent source and test `dotnet format --verify-no-changes`: passed; and
- pre-commit staged diff check and post-commit `git show --check`: passed.

Synthetic regression coverage includes exact-root authority, profile
ambiguity, selected-category-only capture, unknown linked-sibling exclusion,
empty selected content, English/case-safe negatives, selected links, file,
directory, path, per-file and total-byte limits, cancellation, late selected
files and empty categories, ignored late unknown siblings, package identity,
and path-free command outcomes.

## Evidence limits

This is local macOS .NET/Flutter and synthetic-filesystem evidence. It is not a
Windows-native proof, a real Resolume user-data validation, a localized-folder
proof, an application/version compatibility proof, a licensing or plugin/media
portability proof, an application reopen/output test, an installer/signing
test, or an equipment/venue test. None may be inferred from these results.

## Publication stop

The implementation and evidence remain local. A separate task must freshly
inspect current `main`, live PR #16, its reviews/checks/comments, permissions,
rulesets, protection, and merge policy; prove the exact candidate scope; and
prepare an old-head-leased source replacement plus retarget/title/body proposal.
Stop again for explicit authorization immediately before any remote mutation.

Do not start PR #17, dispatch a workflow, or access equipment during that
publication preflight.
