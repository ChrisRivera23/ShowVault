# PR #23 bounded local replacement implementation

Date: 2026-08-13

## Result

The approved bounded ProVisionaire Control PLUS compatibility slice is
implemented locally on branch `codex/pr23-bounded-provisionaire-control` in
isolated worktree `/private/tmp/showvault-pr23-implementation.Kd8POR`, directly
from exact remote `main`
`deb36a45ff22906ed9b9d9c614445731ae3bce85`.

Implementation commit
`b66f432f915d99cf5ad3bab3742dfbb88fd7a2d7` has tree
`a5c48c4fe0be6c426fdae62d65343b6ae9a7d180`, 10 files, `+689/-16`,
binary-diff SHA-256
`594b0763508b11b1e1b6992f37c792153bba9fd1d2e5dbf97da527c5e7d7a543`,
and path-list SHA-256
`5e03ef7f5669666c0ab0b02e20bba581deac37dc8643d0973019f4b9dce12f93`.

No GitHub state changed. PR #23 was not published, retargeted, edited, marked
ready, merged, or closed. PR #24 was not started. No workflow was dispatched,
no artifact was retrieved, no equipment was accessed, and no personal or venue
data was used.

## Implemented boundary

The implementation adds one disabled-by-default legacy-Agent Assisted profile:

- option: `YamahaProVisionaireControlProjectRoots`;
- plugin ID: `showvault.yamaha-provisionaire-control`;
- manifest version: `1.0.0`;
- primary format: root-level `.pvcppj`; and
- recognized opaque companion format: `.pvksk`.

The profile uses the existing current Yamaha recovery architecture. It requires
an exact locally configured absolute root, includes the new root group in the
32-root uniqueness and all-profile non-overlap gate, and rejects a child path or
a known primary artifact owned by another Yamaha profile. A descendant
`.pvcppj` marker cannot authorize the parent, and `.pvksk` alone cannot
authorize capture.

The capture path uses retained no-follow root, directory, and file identities;
exact topology, size, and hash validation; file, directory, relative-path,
per-file-byte, total-byte, capture-time, and package-time bounds; cancellation;
authorization recheck; and stable package recapture/reuse. A late addition,
deletion, content replacement, or same-content identity replacement fails
before package publication.

Compatibility evidence names `.pvcppj` as an opaque ProVisionaire Control PLUS
project and `.pvksk` as an opaque ProVisionaire Kiosk controller export. It
states that the controller export does not replace or prove completeness or
compatibility of the editable project. Existing DM3 and TF companion wording
remains profile-specific, and an unknown future companion-evidence profile now
fails closed instead of inheriting TF wording.

Recovery remains local and Assisted. The package requires a new empty
ShowVault-controlled target and operator validation with compatible Yamaha
software away from production. It makes no claim of semantic validity, export
completeness, model identity, software or firmware compatibility, dependency
closure, live-device state, personal-data readiness, or venue readiness.

## Files

1. `docs/YAMAHA_PROVISIONAIRE_CONTROL_ASSISTED_RECOVERY.md`
2. `services/agent/src/ShowVault.Agent/AgentOptions.cs`
3. `services/agent/src/ShowVault.Agent/Plugins/YamahaDspProjectDiscoveryPlugins.cs`
4. `services/agent/src/ShowVault.Agent/Plugins/YamahaSettingsExportDiscoveryPlugins.cs`
5. `services/agent/src/ShowVault.Agent/Program.cs`
6. `services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageWriter.cs`
7. `services/agent/src/ShowVault.Agent/appsettings.json`
8. `services/agent/tests/ShowVault.Agent.Tests/AgentCommandExecutorTests.cs`
9. `services/agent/tests/ShowVault.Agent.Tests/YamahaProVisionaireControlDiscoveryPluginTests.cs`
10. `services/agent/tests/ShowVault.Agent.Tests/YamahaProVisionaireControlRecoveryPackageTests.cs`

The obsolete historical README, roadmap, handoff, recursive scanner, and broad
configuration changes were not carried forward. There are no migrations,
network changes, credential changes, customer UI changes, or device operations.

## Validation

Validation passed on the exact implementation tree:

- focused Control PLUS discovery/recovery/executor safeguards: 35/35;
- complete Agent suite: 285/285;
- Agent contracts: 22/22;
- platform: 15/15 on a clean serial rerun;
- API: 11/11;
- Flutter analysis: no issues;
- Flutter tests: 16/16;
- Agent Release build: 0 warnings, 0 errors;
- Agent source formatting: clean;
- Agent test formatting: clean; and
- Git diff checks: clean.

Synthetic coverage includes case-insensitive primary and companion formats,
Kiosk-only rejection, nested-marker rejection, exact authorization, sibling
exclusion, root/ancestor/child links, mixed Yamaha primaries, all structural and
byte bounds, fail-instead-of-truncate behavior, empty directories,
cancellation, stable package reuse, authorization recheck, four late-source
mutations, honest compatibility evidence, and successful/failed path-free Agent
outcomes.

The filesystem tests ran on macOS. They do not establish native Windows
reparse-point behavior or Yamaha application/hardware compatibility.

## Next authorization gate

This local implementation is complete, but it is not authorization to publish
or mutate PR #23. The next task, only after explicit authorization, is a fresh
read-only/local publication preflight against then-current `main`, live PR #23,
the exact candidate head, workflow blobs, review surfaces, permissions, and
merge policy. That task may prepare an exact title/body and guarded publication
sequence, but must stop before any remote mutation.

Do not push, force-update, retarget, edit, mark ready, merge, close, or otherwise
mutate PR #23 without a later explicit mutation authorization. Do not start PR
#24, dispatch the Controlled Windows workflow, retrieve artifacts, access
equipment, or use personal/venue data.
