# PR #21 bounded local implementation evidence — 2026-08-12

## Result

The approved local-only replacement for historical PR #21 is implemented on
`codex/pr21-bounded-yamaha-projects`, directly from exact merged `main`
`8f0360c4e89d36e43c47b86dbdd86cb203520344`.

Implementation commit:
`0d03aa4` (`feat: add bounded Yamaha project recovery`). Its tree is
`01ec6897f5e15300e2676046eac38a25f6c16dac`.

The implementation comparison is 9 files, `+621/-3`, with binary-diff SHA-256
`0641548d5def996e24c3361b5bc2d8f858aade9eebbec7a7db73dc2dd69bdf65`
and path-list SHA-256
`494b821aa2302c09481741a4a12bb9d3fb9977a44eb9da06c5ffb03015748ca0`.

## Implemented boundary

- Two disabled-by-default legacy-Agent Assisted profiles use distinct IDs and
  version `1.0.0`: generic Yamaha ProVisionaire Design `.pvd`, and Yamaha MTX/
  MRX Editor `.mtx`.
- DME7 is documented only as an operator-asserted ProVisionaire Design use
  case. Neither extension is parsed or treated as proof of semantic content,
  device identity, completeness, firmware, editor compatibility, or dependency
  closure.
- Each profile accepts at most 32 unique absolute exact roots. Same, ancestor,
  descendant, or duplicate roots are rejected within a profile and across all
  seven Yamaha profiles.
- A recognized primary artifact must be at the configured root. Descendant
  markers do not authorize a parent tree, and any known primary artifact owned
  by another Yamaha profile makes capture fail closed.
- The selected directory is an operator-created dedicated staging root.
  Regular files within it are preserved as opaque operator-selected companions,
  without claiming that Yamaha software requires them.
- Capture reuses the retained no-follow source snapshot. It rejects root,
  ancestor, and child links, non-regular/substituted entries, empty topology,
  and partial success.
- Production bounds remain 4,096 files, 1,024 directories, 1,024 relative-path
  characters, 2 GiB per file, 16 GiB aggregate, two minutes for discovery, and
  fifteen minutes for packaging. Cancellation and a lower requested file cap
  remain authoritative.
- Package creation rechecks exact authorization and family structure, recaptures
  the exact tree, validates topology/hash/identity stability, copies from held
  handles, and refuses stale publication or reuse after a late add, delete,
  content replacement, or same-content identity replacement.
- Agent outcomes stay path-free. Restore prerequisites permit only a new empty
  ShowVault-controlled target followed by operator import/open and validation
  using compatible Yamaha software; the Agent never restores directly into a
  live Yamaha application tree or device.

Historical README, roadmap, catalog, handoff, API, persistence, migration,
Flutter, workflow, network, USB, live-device, and customer-onboarding changes
were not carried into this slice.

## Permanent adversarial coverage

New tests cover both IDs/versions; upper/lowercase root-level formats; exact
root/child authorization; descendant-marker rejection; all-seven-family mixed
primary rejection and overlap; linked root/ancestor/child refusal; file,
directory, path, per-file-byte, aggregate-byte, time-bound constants, and
cancellation behavior; honest opaque compatibility rules; new-empty-target
restore evidence; path-free command outcomes; sibling exclusion; package-time
authorization/structure rechecks; successful stable reuse; and late addition,
deletion, changed-content replacement, and same-content identity replacement.

The shared retained-snapshot and Yamaha regression suites continue to cover
Unix non-regular entries, empty directories, root identity substitution,
rename/resize/rehash drift, stable copy handles, package publication cleanup,
verification, restore, and the five earlier Yamaha families.

## Validation

Final validation in the isolated implementation worktree passed:

- new PR #21 focused tests: 29 passed;
- Yamaha-filtered Agent tests: 99 passed;
- complete Agent tests: 251 passed, 0 failed, 0 skipped;
- Agent contracts: 22 passed;
- platform: 15 passed;
- API: 11 passed;
- Entity Framework pending-model gate: no model changes;
- Flutter dependency resolution: passed without tracked changes;
- Flutter analysis: no issues;
- Flutter tests: 16 passed;
- Agent Release build with warnings as errors: 0 warnings, 0 errors;
- Agent source/test format verification: passed; and
- JSON, diff whitespace, unchanged-workflow, privacy-string, commit, and tree/
  diff/path identity checks: passed.

## Evidence limits and stop gate

This is synthetic local macOS filesystem, .NET, and Flutter evidence. Existing
shared tests exercise Unix no-follow behavior; no Windows-native reparse,
vendor-application, hardware, personal-data, or venue evidence was produced.

This task stops locally. Nothing was pushed; PR #21 was not edited, retargeted,
marked ready, merged, closed, or otherwise mutated; PR #22 was not started or
mutated; no workflow was dispatched; no artifact was retrieved; no equipment
or external target was contacted; and no personal, customer, or venue data was
accessed.
