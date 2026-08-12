# PR #20 bounded local implementation evidence — 2026-08-12

## Result

The approved local-only replacement for historical PR #20 is implemented on
`codex/pr20-bounded-dm3-exports`, directly from exact merged `main`
`df9a17eb0c817581e104570ad5d9dfc7c0bde806`.

Implementation commit:
`4e10ef5db3f32dc4b493e54522577e9370f122f4`
(`feat: add bounded Yamaha DM3 recovery`). Its tree is
`bfa36b275f201ffdd4ac5ed8762e9694026a0242`.

The implementation comparison is 9 files, `+396/-20`, with binary-diff
SHA-256 `d5182113ba28a86017e64ffb20bbd0594535af963a983791607ed3308cb33c18`
and path-list SHA-256
`ec2b0053ec59051a400d78887f21f61184d339bcc6052607007614ce0c5dcdba`.

## Implemented boundary

- One disabled-by-default Yamaha DM3 settings-export Assisted recovery profile
  uses ID `showvault.yamaha-dm3-settings-export`, version `1.0.0`.
- The profile accepts no more than 32 unique absolute exact directories. Equal,
  parent, and descendant overlap within or among all five Yamaha profiles is
  rejected.
- DM3 requires a root-level `.DM3F`. `.DM3S` scene and `.DM3P` preset files are
  retained as opaque companions but do not authorize a settings-export root.
- A selected directory containing another supported Yamaha family's primary
  settings format is rejected instead of being packaged as a mixed
  compatibility target.
- Capture uses retained no-follow directory and regular-file handles and
  identities. It rejects links/reparse traversal, non-regular entries, empty
  topology, and partial/truncated success.
- Production bounds remain 4,096 files, 1,024 directories, 1,024 relative-path
  characters, 2 GiB per file, 16 GiB aggregate, two minutes for discovery, and
  fifteen minutes for packaging. A lower requested file limit remains
  authoritative.
- Package creation rechecks authorization and family structure, recaptures the
  exact tree, requires unchanged topology, sizes, hashes, and retained
  identities, copies through retained handles, and revalidates before immutable
  publication or reuse.
- Compatibility rules identify the opaque Yamaha DM3 family and `.DM3F`
  primary format. `.DM3S`/`.DM3P` companion evidence explicitly does not claim
  completeness or compatibility.
- Restore remains attended into a new empty ShowVault-controlled target, with
  compatible Editor or non-production-console validation and a power-down/
  lower-output procedure before loading.

No root README, roadmap or catalog sequencing, Flutter source, API endpoint,
persistence schema, migration, workflow, network or USB enumeration, customer
onboarding, or direct console/Editor integration changed.

## Permanent adversarial coverage

New tests cover the DM3 ID/version, case-insensitive `.DM3F`, root-level
recognition, `.DM3S`/`.DM3P` capture and non-authorization, distinct
authorization, five-profile overlap, mixed DM3/TF and DM3/DM7 rejection,
path-free exact-root command packaging, package compatibility evidence,
package-time authorization and family rechecks, and existing-package refusal
after topology drift.

The shared Yamaha suite continues to cover sibling exclusion, linked root/
ancestor/descendant rejection, a Unix non-regular socket entry, empty topology,
explicit file/directory/path/per-file/aggregate/time bounds, cancellation,
fail-instead-of-truncate behavior, late add/remove/rename/replace/resize/rehash,
root identity replacement, retained handles, and stale-package refusal.
Complete Agent regressions retain package verification, restore, grandMA,
Resolume, DM7, RIVAGE PM, CL/QL, and TF coverage.

## Validation

Final validation in the isolated implementation worktree passed:

- focused Yamaha and DM3 command-path tests: 64 passed;
- complete Agent tests: 220 passed, 0 failed, 0 skipped;
- Agent contracts: 22 passed, 0 failed, 0 skipped;
- platform: 15 passed, 0 failed, 0 skipped;
- API: 11 passed, 0 failed, 0 skipped;
- Entity Framework pending-model gate: no model changes;
- Flutter dependency resolution: passed without tracked changes;
- Flutter analysis: no issues;
- Flutter tests: 16 passed;
- Agent Release build: 0 warnings and 0 errors;
- Agent source and test `dotnet format --verify-no-changes`: passed;
- JSON, diff whitespace, privacy-string, unchanged-workflow, and implementation
  `git show --check` gates: passed.

## Evidence limits and stop gate

This is synthetic local macOS filesystem, .NET, and Flutter evidence. Existing
tests exercise Unix non-regular-entry rejection and the shared no-follow
platform abstraction, but no Windows-native reparse or equipment evidence was
produced here.

This work does not prove real USB behavior, DM3 Editor or console behavior,
firmware compatibility, representative export completeness, DM3 versus DM3
Standard provenance, TF conversion, license or dependency portability,
application load/reopen behavior, signal safety, installer or signing behavior,
or production readiness. Those require separately authorized controlled
evidence and operator/vendor confirmation.

The task stops locally. Nothing was pushed; no pull request was created,
edited, retargeted, marked ready, merged, or closed; no workflow was
dispatched; no artifact was retrieved; no equipment or external target was
contacted; and no personal, customer, or venue data was accessed.
