# PR #24 bounded local replacement implementation

Date: 2026-08-13

## Result

The bounded PR #24 replacement is complete locally on branch
`codex/pr24-bounded-dme5-dme3`, directly from exact remote `main`
`e3afba8caffda0341a31033a6e3e396799cc7406`.

It reuses the existing generic ProVisionaire Design `.pvd` Assisted profile,
recognizes `.pvksk` case-insensitively as an opaque Control PLUS controller
companion, and documents DME5/DME3 only as operator-asserted uses. It adds no
DME5/DME3 scanner, option, plugin ID, registration, configuration root, or
model inference.

No GitHub state changed. No workflow was dispatched, no artifact was retrieved,
no equipment was accessed, and no personal or venue data was used.

## Exact implementation identity

- Base: `e3afba8caffda0341a31033a6e3e396799cc7406`
- Branch: `codex/pr24-bounded-dme5-dme3`
- Implementation commit:
  `b21958d87268f3b7147832b72966c7381b60fe1f`
- Implementation tree:
  `fb2fc472ee889f20ecf82be677e6e9ddb111e29e`
- Scope: 5 files, `+93/-7`
- Binary-diff SHA-256:
  `97a8ac823c179f7826aadd3df5cafaf4b4f9f48c6d12ffbeea4fc35f4b72c359`
- Path-list SHA-256:
  `6b00bf2fdd9ebf2cd4a513466b3fc772f82db114705ab9137d8b3e948774122e`

Changed paths:

1. `docs/YAMAHA_DSP_PROJECT_ASSISTED_RECOVERY.md`
2. `services/agent/src/ShowVault.Agent/Plugins/YamahaSettingsExportDiscoveryPlugins.cs`
3. `services/agent/src/ShowVault.Agent/Recovery/RecoveryPackageWriter.cs`
4. `services/agent/tests/ShowVault.Agent.Tests/YamahaDspProjectDiscoveryPluginTests.cs`
5. `services/agent/tests/ShowVault.Agent.Tests/YamahaDspProjectRecoveryPackageTests.cs`

## Bounded behavior

The implementation retains
`showvault.yamaha-provisionaire-design-project` and
`YamahaProVisionaireDesignProjectRoots`. A root-level `.pvd` remains mandatory;
a `.pvksk` alone cannot authorize capture. The existing profile continues to
enforce exact configured roots, cross-profile separation, root-level primary
recognition, no-follow retained identities, exact topology/size/hash
revalidation, file/directory/path/byte/time/cancellation bounds, stable package
reuse, mixed-primary rejection, path-free outcomes, and new-empty-target
Assisted restore.

When a `.pvksk` is present anywhere inside an authorized `.pvd` staging root,
the package records it as an opaque Control PLUS controller companion. Evidence
states that it was created separately, may support a DME5/DME3 Custom Control
Panel workflow, and does not prove:

- the DME model;
- controller transfer;
- `.pvd` project completeness; or
- hardware, firmware, software, browser, or network compatibility.

The documentation identifies DME7, DME5, DME3, and PC-D/DI as operator-
asserted uses of the same generic `.pvd` profile and requires exact model and
version validation. It distinguishes the separately created Control PLUS
controller file from the ProVisionaire Design project.

## Validation

Validation passed on the exact implementation tree:

- focused Yamaha DSP and Control PLUS discovery/package suite: 67/67;
- complete Agent suite: 291/291;
- Agent contracts: 22/22;
- platform: 15/15;
- API: 11/11;
- Flutter analysis: no issues;
- Flutter tests: 16/16;
- Agent Release build with warnings as errors: 0 warnings, 0 errors;
- Agent source formatting: clean;
- Agent test formatting: clean; and
- Git diff checks: clean.

New focused tests prove lowercase and uppercase `.pvksk` preservation,
companion classification, rejection without `.pvd`, package inclusion,
path-free compatibility evidence, and wording that does not infer that the
project contains a DME5 or DME3.

The filesystem tests ran on macOS. They do not establish native Windows
reparse-point behavior, Yamaha application import, controller transfer,
firmware compatibility, hardware restore, personal-data readiness, or venue
readiness.

## Historical changes intentionally dropped

The replacement does not carry forward the historical README, roadmap,
handoff, `YamahaDme5Dme3ProjectRoots` option, standalone DME5/DME3 plugin,
registration, appsettings entry, obsolete inherited scanner, or obsolete test
file. A repository search confirms no `YamahaDme5`, `Dme5Dme3`,
`yamaha-dme5`, or `dme5-dme3` source/test identifier was introduced.

## Authorization boundary and next task

This local implementation authorizes no remote mutation. The next task, only
after explicit authorization, is a read-only PR #24 publication preflight that
pins the live old source/base, exact candidate scope and hashes, safe lease-
guarded replacement sequence, corrected title/body proposal, CI requirements,
and downstream effects.

Do not push or mutate PR #24, dispatch a workflow, retrieve artifacts, access
equipment, or use personal/venue data without the applicable later gate.
