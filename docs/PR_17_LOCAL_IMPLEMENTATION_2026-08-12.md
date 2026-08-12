# PR #17 bounded local implementation evidence

## Result

The approved replacement/narrowed grandMA show-export Assisted recovery slice
is implemented locally on `codex/pr17-bounded-grandma-exports`, directly from
exact merged PR #16 `main`
`4e8909a2ee2b6adc8d491b73d75a6bd9b1ea35db`, tree
`56c991ea3518f69997ea1978b5209323792e7aff`.

Implementation commit:

- commit: `032258d0433476dafb8ae58341ab74c2e6025477`;
- tree: `5da43c8b19fd11a83547b7bb62efcdaa93b40cf7`;
- scope: 11 files, `+1,125/-22`;
- binary-diff SHA-256:
  `ca7b50b22fd766bded9b31d053e6131b882438a4cbd143e8e0b4dd784a6f4cea`;
- path-list SHA-256:
  `4a2d5ce006d67a39606e35221bed5ca03fdb4aa655a1d7780ccd14ca3e6bd4fa`.

No GitHub state, workflow, artifact, external host, customer/personal/venue
data, or vendor equipment was accessed or changed.

## Implemented product boundary

The implementation adds two distinct disabled-by-default legacy-Agent Assisted
profiles:

- `showvault.malighting-grandma2-show-export` version `1.0.0`;
- `showvault.malighting-grandma3-show-export` version `1.0.0`.

`GrandMa2ShowExportRoots` and `GrandMa3ShowExportRoots` each accept at most 32
unique absolute exact roots. Startup validation rejects duplicates and overlap
between product profiles. The plugins repeat exact-root authorization at
discovery, and packaging rechecks both authorization and recognized structure.

The authority boundary is the operator-selected leaf, never the entire product
tree:

- grandMA2: exact `gma2/shows` or
  `gma2/<major.minor-or-patch>/shows`;
- grandMA3: exact `grandMA3/shared/shows` or
  `grandMA3/shared/backups`.

Everything outside that exact leaf remains unopened and uncollected. This
excludes `gma3_library`, users, certificates, plugins, media, netkeys, licenses,
credentials, logs, crash data, screenshots, temporary data, and unrelated
siblings. No disk, profile, application-installation, console, network, or FTP
search was added.

For grandMA2 versioned paths, the version directory is stored as protected
manifest product-version evidence. The grandMA3 path does not encode a product
version, so the manifest records that operator confirmation and application
validation remain required. Both profiles add the vendor forward-only
compatibility constraint and an explicit no-live-tree restore prerequisite.

## Filesystem and package safety

The stable-source implementation now has a read-only absolute-path opener that
walks components no-follow instead of opening only the final component. It
rejects arbitrary linked ancestors, roots, descendants, reparse points, devices,
and other non-regular entries. On macOS only the fixed system aliases `/etc`,
`/tmp`, and `/var` are translated to their `/private/...` filesystem locations
before the no-follow walk; arbitrary user/configured links remain rejected.

Root, directory, and regular-file identities and handles are retained during
capture. The tree is re-opened and identities, topology, sizes, and hashes are
revalidated. Packaging independently recaptures the exact authorized profile,
requires an exact manifest file set, copies from held file handles, and repeats
stable validation before immutable publication. Added, removed, renamed,
replaced, resized, or rehashed entries fail closed, as do empty directories that
cannot be represented by the current file-only manifest.

Profile limits are:

- 4,096 files;
- 1,024 directories;
- 1,024 characters per relative path;
- 2 GiB per file;
- 16 GiB total;
- two minutes for discovery; and
- fifteen minutes for package creation.

Requested lower file limits remain authoritative. Any bound or cancellation
failure returns no partial inventory; truncated success is never emitted.

Command completion and failure events remain path-free. Exact roots, filenames,
timestamps, sizes, and hashes remain in protected local recovery/package
records. The executor emits bounded failure categories and does not serialize
exception text into outbound events or logger messages.

## Restore boundary

Restore was not broadened. A grandMA recovery package can use only the existing
attended verified-package restore flow into a new empty ShowVault-controlled
target. Manifest prerequisites require subsequent operator placement/import
through the vendor workflow and explicitly prohibit direct restore into a live
console or onPC tree.

This implementation does not protect or claim dependency closure for VPU/media,
software, firmware, drivers, networking, plugins, licenses, certificates,
secrets, or users. It does not prove that an export will load, reopen, or
perform correctly on a real product version.

## Changed paths

- `docs/MA_LIGHTING_ASSISTED_RECOVERY.md`
- `services/agent/src/ShowVault.Agent/AgentOptions.cs`
- `services/agent/src/ShowVault.Agent/Plugins/MaLightingShowExportDiscoveryPlugins.cs`
- `services/agent/src/ShowVault.Agent/Program.cs`
- `services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageWriter.cs`
- `services/agent/src/ShowVault.Agent/Recovery/StableDirectoryTree.cs`
- `services/agent/src/ShowVault.Agent/Recovery/StableSourceSnapshot.cs`
- `services/agent/src/ShowVault.Agent/appsettings.json`
- `services/agent/tests/ShowVault.Agent.Tests/AgentCommandExecutorTests.cs`
- `services/agent/tests/ShowVault.Agent.Tests/MaLightingShowExportDiscoveryPluginTests.cs`
- `services/agent/tests/ShowVault.Agent.Tests/MaLightingShowExportRecoveryPackageTests.cs`

There are no root README, roadmap, Flutter source, API endpoint, persistence
schema, migration, workflow, network-discovery, customer onboarding, or direct
desktop scan changes.

## Validation

Final validation from the isolated exact-main worktree passed:

- focused grandMA tests: 19 passed;
- Agent: 152 passed, 0 failed, 0 skipped;
- contracts: 22 passed, 0 failed, 0 skipped;
- platform: 15 passed, 0 failed, 0 skipped;
- API: 11 passed, 0 failed, 0 skipped;
- Entity Framework pending-model gate: no model changes;
- Flutter dependency resolution: passed without tracked changes;
- Flutter analysis: no issues;
- Flutter: 16 passed;
- Agent Release build: 0 warnings and 0 errors;
- Agent source and test `dotnet format --verify-no-changes`: passed;
- staged diff check and post-commit `git show --check`: passed.

Synthetic grandMA coverage includes exact-root authorization, distinct plugin
identity/version, grandMA2 version evidence, grandMA3 honest unknown-version
evidence, exact show/backup leaf capture, `gma3_library` and sibling exclusion,
case/structure negatives, linked root/ancestor/descendant rejection, non-exact
parent rejection, empty topology, file and requested-count bounds, cancellation,
late file, root identity swap, package-time authorization recheck, assisted
restore/compatibility rules, immutable content, and both success/failure
path-free outcomes. Existing stable-source regressions continue to cover
directory, relative-path, aggregate-byte, identity, content, and cancellation
bounds used by this profile.

One exploratory focused run initially exposed macOS's standard `/var` alias.
The final implementation handles only the fixed platform aliases explicitly;
all final focused and regression runs above passed after that correction.

## Evidence limits

This is local macOS .NET/Flutter and synthetic-filesystem evidence. It is not a
Windows-native/reparse proof, real removable-media proof, localized-folder
proof, representative grandMA version proof, application load/reopen test,
license/media/plugin portability proof, installer/signing proof, or equipment
or venue test. None may be inferred from these results.

## Publication stop

The implementation and evidence remain local. A separate task must freshly
inspect exact current `main`, live PR #17, its checks/reviews/comments,
permissions, rulesets/protection, merge policy, and dependent PR #18; prove the
candidate scope; and prepare an old-head-leased source replacement plus
retarget/title/body proposal. Stop again for explicit authorization immediately
before any push or PR mutation.

Do not start PR #18, dispatch a workflow, retrieve an artifact, or access
equipment during that publication preflight.
