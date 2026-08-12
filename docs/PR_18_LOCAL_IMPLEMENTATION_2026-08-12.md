# PR #18 bounded local implementation evidence — 2026-08-12

## Result

The approved local-only PR #18 replacement is implemented on
`codex/pr18-bounded-yamaha-exports`, directly from exact current `main`
`68d9920b409b205404cb881ab2f5688b3e54ed2f`.

Implementation commit:
`bdf5795c4fe98394e252c2b5308b6437ee707b9e`
(`feat: recover bounded Yamaha settings exports`). Its tree is
`6bc4dc992d058473c55982913f6aad20bfb0dcd0`.

The implementation comparison is 9 files, `+1,226/-33`, with binary-diff
SHA-256 `7540ea34862e7583d966a7a56fa3e88c3efd43bf4091efe8161c53e8f7cca4b1`
and path-list SHA-256
`88323a898bfaaef343a4dce947d67293d3e19bf20fbf3d56eda6f6b155d15082`.

## Implemented boundary

- Separate disabled-by-default DM7 and RIVAGE settings-export Assisted
  profiles use IDs `showvault.yamaha-dm7-settings-export` and
  `showvault.yamaha-rivage-settings-export`, both at version `1.0.0`.
- Each profile accepts no more than 32 unique absolute exact directories.
  Cross-profile equal, parent, and descendant overlap is rejected.
- DM7 recognizes a root-level `.dm7f`; RIVAGE recognizes root-level
  `.RIVAGEPM`, `.PM10ALL`, `.PM7ALL`, `.PM10PART`, and `.PM7PART`.
  A marker below the selected root does not authorize capture.
- Capture uses the existing retained no-follow directory and regular-file
  handles and identities. It rejects links/reparse traversal, non-regular
  entries, empty topology, and partial/truncated success.
- Production bounds are 4,096 files, 1,024 directories, 1,024 relative-path
  characters, 2 GiB per file, 16 GiB aggregate, two minutes for discovery, and
  fifteen minutes for packaging. A lower requested file limit remains
  authoritative.
- Package creation rechecks local authorization and root-level recognition,
  recaptures the exact tree, requires the exact discovered topology, sizes,
  hashes, and identities, copies through retained handles, and revalidates
  before immutable publication. The same checks now precede Yamaha reuse of an
  already-existing content-addressed package.
- Manifest rules identify the opaque Yamaha family and recognized format and
  require operator confirmation of source family/model/version, export
  completeness, and destination compatibility. They explicitly disclaim
  semantic, firmware, license, plug-in, external dependency, and safe-load
  proof.
- Restore remains attended into a new empty ShowVault-controlled target, with
  subsequent compatible Editor or non-production validation and Yamaha's
  power-down/lower-output warning before loading.
- Command success and authorization-failure tests confirm path-free outcomes
  and logs. Exact paths and file evidence remain local to protected records.

No root README, roadmap, catalog sequencing, PR #19+ Yamaha family, Flutter
source, API endpoint, persistence schema, migration, workflow, network or USB
enumeration, customer onboarding, or direct console/Editor integration changed.

## Permanent adversarial coverage

The new tests cover both profile IDs, versions, formats, distinct authorization
lists, exact-root inventory, sibling exclusion, descendant-only marker
rejection, duplicate and overlapping configuration, linked root/ancestor/
descendant rejection, a Unix non-regular socket entry, empty topology, explicit
file/directory/path/per-file/aggregate/time bounds, cancellation, and
fail-instead-of-truncate behavior.

Package tests cover format/family evidence, partial-export honesty, attended
restore and output-signal prerequisites, package-time authorization and
structure rechecks, late addition/removal/rename/replacement/resize/rehash,
root identity replacement, and refusal to return a stale already-existing
package after source drift. Existing complete Agent regressions retain the
shared stable-source, package verification, restore, grandMA, and Resolume
coverage.

## Validation

Final validation in the isolated implementation worktree passed:

- focused Yamaha and command-path tests: 38 passed;
- complete Agent tests: 190 passed, 0 failed, 0 skipped;
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

This is synthetic local macOS filesystem, .NET, and Flutter evidence. The Unix
socket test directly exercises local non-regular-entry rejection; existing
implementation uses the same no-follow platform abstraction on Windows, but no
Windows-native reparse/equipment evidence was produced here.

This work does not prove real USB behavior, Yamaha Editor or console behavior,
firmware compatibility, representative export completeness, license or plug-in
portability, application load/reopen behavior, signal safety, installer or
signing behavior, or production readiness. Those require separately authorized
controlled evidence and operator/vendor confirmation.

The task stops locally. Nothing was pushed; PR #18 was not edited, retargeted,
marked ready, merged, or closed; no workflow was dispatched; no artifact was
retrieved; no equipment or external host was contacted; and no personal,
customer, or venue data was accessed.
