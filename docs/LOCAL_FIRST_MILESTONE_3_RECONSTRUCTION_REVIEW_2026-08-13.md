# Milestone 3 bounded reconstruction review — 2026-08-13

## Result

The historical synchronization/Restore slice was reviewed locally from
immutable Git objects. The selected milestone-3 outcome is **controlled local
Restore and path-free recovery evidence**. Hosted synchronization is deferred
to the following roadmap slice. The historical Restore disposition is
**replace/narrow**, not replay.

The controlling implementation contract is
`docs/LOCAL_FIRST_MILESTONE_3_EXTRACTION.md`.

## Exact source accounting

The containing range is `c172e49..fff4434`: ten commits, 31 net paths,
`+5,387/-76`, binary-diff SHA-256
`7cb9d0c81ac5646353c9645eefd86844afa9706c8569fb2595afa241d188a317`,
and path-list SHA-256
`751bd1a7eaceee71b89fd1a798ea4514acba92586c1e5479ebdae55a346ae0eb`.

Restore behavior comes from exact commits `36fcda9` and `a62649f`. Their union
touches nine paths; the two synchronization/configuration paths in `a62649f`
are excluded. The seven Restore-relevant paths include five historical
source/test additions and two current dashboard/app-test overlaps that require
reconciliation.

## Retained product evidence

- Restore is attended, signed-out, offline, and verified-point-only.
- The operator selects an empty sandbox independently from the vault.
- The final corrected layout publishes only the fixed
  `ShowVault Restored Files` child inside the selected sandbox.
- Restore stages, hashes copied bytes, verifies the published tree, supports
  Cancel, writes path-free evidence, and refuses to load a live application or
  device.
- Owned interrupted staging may be resumed or cleaned; unowned content must be
  preserved.

## Blocking historical behavior

The Dart engine reopens verified package and target paths without retained
identities, performs recursive cleanup through mutable pathnames, lacks a
complete durable restart state machine, does not close mount/hard-link and
ancestor-swap races, exposes an evidence path in its result, and duplicates the
milestone-2 local engine. The complete range also mixes Restore with hosted
synchronization and installed evidence.

The replacement extends the packaged .NET local engine, retains package and
target identities throughout, publishes one fixed child without replacing the
selected target, records path-free durable evidence, and supports bounded
reselect-and-repair semantics. Flutter remains consent/status only.

## Reusable current primitives

The current local engine already owns verified vault inspection, exact package
rehashing, retained no-follow directory/file operations, bounds, cancellation,
SQLite durability, path-free host records, and quarantine repair. The current
Agent `RecoveryPackageRestorer` supplies useful retained staging/publication
concepts, but Agent configured roots, command identity, queue store, result
paths, and service lifecycle are explicitly excluded.

## Authorization record

The Product Owner authorized starting the next task after milestone 2. Per the
milestone-2 handoff and ordered roadmap, that authorization is applied to this
bounded milestone-3 extraction/reconstruction plan. No Restore product source,
hosted synchronization, external/native action, or personal/customer/venue
data access is authorized or performed by this planning task.
