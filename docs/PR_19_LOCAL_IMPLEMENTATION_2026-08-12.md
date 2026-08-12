# PR #19 bounded local implementation evidence — 2026-08-12

## Result

The approved local-only replacement for historical PR #19 is implemented on
`codex/pr19-bounded-clql-tf-exports`, directly from exact merged `main`
`c2414d872e0ca6179a9dd544b48c72822ceb8e47`.

Implementation commit:
`781039d88dbaa86669d80cb593497a63abde4fff`
(`feat: add bounded Yamaha CL QL and TF recovery`). Its tree is
`349d09c6b4c3b3cfda840e97215601718316d879`.

The implementation comparison is 9 files, `+617/-28`, with binary-diff
SHA-256 `17a5f81dcca2992e4e02bd5496100dc87b127f255bc4fae926e1fb7aeee8a35f`
and path-list SHA-256
`f1c60637ae54bec95457e7fc9152905a5482c222424282d56a901b8d82218cdd`.

## Implemented boundary

- Separate disabled-by-default CL/QL and TF settings-export Assisted recovery
  profiles use IDs `showvault.yamaha-cl-ql-settings-export` and
  `showvault.yamaha-tf-settings-export`, both at version `1.0.0`.
- Each profile accepts no more than 32 unique absolute exact directories.
  Equal, parent, and descendant overlap within or among all four supported
  Yamaha profiles is rejected.
- CL/QL requires a root-level `.CLF`; TF requires a root-level `.TFF`.
  TF `.TFP` preset and `.TFS` scene files are retained as opaque companions
  but do not authorize a settings-export root.
- A selected directory containing another supported Yamaha family's primary
  format is rejected instead of being packaged as a mixed compatibility
  target.
- Capture uses retained no-follow directory and regular-file handles and
  identities. It rejects links/reparse traversal, non-regular entries, empty
  topology, and partial/truncated success.
- Production bounds remain 4,096 files, 1,024 directories, 1,024
  relative-path characters, 2 GiB per file, 16 GiB aggregate, two minutes for
  discovery, and fifteen minutes for packaging. A lower requested file limit
  remains authoritative.
- Package creation rechecks authorization and family structure, recaptures the
  exact tree, requires unchanged topology, sizes, hashes, and retained
  identities, copies through retained handles, and revalidates before
  immutable publication or reuse.
- Compatibility rules identify the opaque Yamaha family and primary formats.
  TF companion evidence explicitly does not claim `.TFF` completeness.
- Restore remains attended into a new empty ShowVault-controlled target, with
  compatible Editor or non-production-console validation and Yamaha's
  power-down/lower-output warning before loading.

No root README, roadmap or catalog sequencing, Flutter source, API endpoint,
persistence schema, migration, workflow, network or USB enumeration, customer
onboarding, or direct console/Editor integration changed.

## Permanent adversarial coverage

New tests cover both profile IDs and versions, case-insensitive primary
formats, TF companion capture and non-authorization, distinct authorization
lists, same- and cross-profile root overlap, foreign Yamaha primary rejection,
exact-root command success, path-free authorization failure, compatibility
evidence, package-time authorization and family rechecks, source drift before
reuse, and existing-package refusal after drift.

The pre-existing Yamaha suite continues to cover sibling exclusion,
descendant-only marker rejection, linked root/ancestor/descendant rejection, a
Unix non-regular socket entry, empty topology, explicit file/directory/path/
per-file/aggregate/time bounds, cancellation, fail-instead-of-truncate
behavior, late filesystem mutations, root identity replacement, retained
handles, and stale-package refusal. Complete Agent regressions retain package
verification, restore, grandMA, Resolume, DM7, and RIVAGE coverage.

## Validation

Final validation in the isolated implementation worktree passed:

- focused Yamaha and command-path tests: 55 passed;
- complete Agent tests: 207 passed, 0 failed, 0 skipped;
- Agent contracts: 22 passed, 0 failed, 0 skipped;
- platform: 15 passed, 0 failed, 0 skipped;
- API: 11 passed, 0 failed, 0 skipped;
- Entity Framework pending-model gate: no model changes;
- Flutter dependency resolution: passed without tracked changes;
- Flutter analysis: no issues;
- Flutter tests: 16 passed;
- Agent Release build: 0 warnings and 0 errors;
- Agent source and test `dotnet format --verify-no-changes`: passed;
- diff whitespace, privacy-string, unchanged-workflow, and implementation
  `git show --check` gates: passed.

## Evidence limits and stop gate

This is synthetic local macOS filesystem, .NET, and Flutter evidence. Existing
tests exercise Unix non-regular-entry rejection and the shared no-follow
platform abstraction, but no Windows-native reparse or equipment evidence was
produced here.

This work does not prove real USB behavior, Yamaha Editor or console behavior,
firmware compatibility, representative export completeness, license or
plug-in portability, application load/reopen behavior, signal safety,
installer or signing behavior, or production readiness. Those require
separately authorized controlled evidence and operator/vendor confirmation.

The task stops locally. Nothing was pushed; no pull request was created,
edited, retargeted, marked ready, merged, or closed; no workflow was
dispatched; no artifact was retrieved; no equipment or external host was
contacted; and no personal, customer, or venue data was accessed.
