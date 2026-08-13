# Local-first milestone 3 extraction and architecture contract

## Outcome

Milestone 3 adds the first bounded, attended desktop Restore path after the
milestone-2 local recovery point is independently reverified:

**Open local vault → select a verified recovery point → Restore or Cancel →
verify the restored copy → retain path-free local evidence**

Restore remains available while signed out and offline. It copies files only
into an explicitly selected empty sandbox. It does not load files into a
running application or device, synchronize/upload, require a cloud receipt,
calculate Recovery Confidence, use personal/customer/venue data in tests, or
expose Agent installation, enrollment, service, or command controls.

This contract authorizes no implementation or external action by itself.

## Historical source accounting and disposition

The complete historical containing range is exact `c172e49..fff4434`, ten
commits, 31 net paths, `+5,387/-76`, binary-diff SHA-256
`7cb9d0c81ac5646353c9645eefd86844afa9706c8569fb2595afa241d188a317`,
and sorted path-list SHA-256
`751bd1a7eaceee71b89fd1a798ea4514acba92586c1e5479ebdae55a346ae0eb`.

| Commit | Historical concern | Milestone-3 disposition |
| --- | --- | --- |
| `f016ad1` | durable synchronization executor | defer to hosted synchronization |
| `378acce` | attended-Restore starting handoff | regenerate |
| `36fcda9` | Dart verified local Restore | extract product behavior; replace engine |
| `e8819cc` | authenticated-sync handoff | defer/regenerate |
| `5f05f44` | hosted client/API/filesystem store | defer to hosted synchronization |
| `e980165` | installed-drill starting handoff | historical evidence only |
| `a62649f` | selected-sandbox Restore correction | retain final sandbox behavior; replace engine |
| `97b56a0` | installed hosted drill | historical evidence only |
| `a7eee0d` | synchronized-status refresh | defer to hosted synchronization |
| `fff4434` | installed-drill handoff | regenerate |

The two Restore-bearing commits touch nine unique paths. Seven are
Restore-relevant; two mixed `a62649f` paths—`app_config.dart` and
`local_sync_service.dart`—are synchronization concerns and are dropped. Of the
seven relevant paths, the dashboard and app test overlap the current
milestone-2 branch and must be reconciled, never transplanted. Across the full
range, the current branch overlaps only the root README, continuation handoff,
dashboard, app test, and readiness document.

Reproduce the immutable accounting with:

```bash
test "$(git rev-list --count c172e49..fff4434)" = 10
test "$(git diff --name-only c172e49..fff4434 | sort -u | wc -l | tr -d ' ')" = 31
test "$(git diff --binary c172e49..fff4434 | shasum -a 256 | cut -d' ' -f1)" = \
  7cb9d0c81ac5646353c9645eefd86844afa9706c8569fb2595afa241d188a317
test "$(git diff --name-only c172e49..fff4434 | sort -u | shasum -a 256 | cut -d' ' -f1)" = \
  751bd1a7eaceee71b89fd1a798ea4514acba92586c1e5479ebdae55a346ae0eb
```

## Why the historical implementation cannot be replayed

The old Dart implementation proves the intended attended flow and the final
`ShowVault Restored Files` sandbox child, but it is not an acceptable current
filesystem boundary:

- it verifies and later reopens package directories/files by pathname without
  retaining identities across verification and copy;
- target canonicalization is separated from later path-based staging,
  enumeration, deletion, and rename operations;
- linked or swapped ancestors, mount substitutions, hard-link aliases, target
  replacement, and late target entries are not closed by retained handles;
- recursive staging cleanup relies on a pathname marker and can race an
  ownership swap;
- it publishes restored bytes before durable completion evidence without a
  complete restart-safe transactional state machine;
- its result exposes an evidence filesystem path;
- it accepts an absent programmatic target beyond the customer picker flow;
- it duplicates verification/Restore behavior in Flutter instead of extending
  the single packaged local engine; and
- the surrounding range couples Restore to synchronization, hosted storage,
  installed evidence, and configuration that are outside this milestone.

The approved disposition is **replace/narrow**.

## Architectural decision: extend the packaged local engine

Filesystem verification, target authorization, staged copy, publication,
post-publication verification, cleanup, restart repair, and evidence
persistence remain in `ShowVault.LocalEngine`. Reuse and narrow the current
Agent's hardened retained-handle Restore primitives where appropriate, but do
not depend on Agent identity, configured Restore roots, commands, credentials,
queues, network connectivity, or service lifecycle.

Flutter remains the native consent and status surface. The packaged host adds
only closed Restore and Restore-cancel records. It must not accept arbitrary
commands, unrestricted output names, plugin assemblies, network targets,
application/device loading, or a customer-supplied evidence location.

The Restore input is bounded and local-process-only:

- explicitly authorized vault;
- exact recovery-point ID selected from the freshly reverified vault view; and
- explicitly selected, existing empty Restore sandbox.

The path-free result contains only recovery-point ID, Restore evidence ID,
file/byte counts, completion time, and a closed local status/error code.

## Reconstruction order

### 1. Restore contracts and exact eligibility

Add a small local-engine contract for recovery-point identity, selected vault,
selected target sandbox, limits, cancellation, progress, and path-free result.

Required behavior:

- only a record returned by fresh `InspectVaultStateAsync` verification may be
  restored;
- queue/cloud state is not treated as content evidence; the package,
  independent manifest, and evidence are reverified again at Restore start;
- the vault and package are opened through retained no-follow identities;
- the target must be a picker-selected existing regular directory, empty
  except for an exactly owned resumable stage for the same recovery point;
- the target and vault must be distinct, non-overlapping canonical trees and
  aliases, with no linked, reparse, mounted, or non-directory component;
- no absent/broad programmatic target, persistent bookmark, or stored target
  path is introduced; and
- package source, target, and vault paths never enter results, errors, evidence,
  logs, queue state, or cloud-facing messages.

### 2. Retained package snapshot and Restore intent

Refactor the milestone-2 verifier only as needed to retain the exact verified
package/content directory and regular-file handles through the copy. Enforce
the existing file, directory, relative-path, per-file, aggregate-byte,
recovery-point, and duration bounds throughout Restore.

Inside the selected target, create a deterministic hidden stage for the
recovery-point identity containing a bounded `intent.json` and `restored/`
tree. The intent binds only the full recovery-point ID, closed format version,
and fixed publication child name; it contains no source, vault, or target path.

Before reusing or removing a stage, require the exact retained stage identity,
complete bounded topology, regular intent file, exact intent contents, and
same-volume ownership. Preserve any unowned, malformed, linked, substituted,
or ambiguous stage and return a path-free attention result.

### 3. Stable copy and one-way sandbox publication

Copy retained package file streams into retained destination identities under
`restored/`. Enforce before and during copying:

- cancellation and deadline checks at enumeration, read, hash, directory,
  file, verification, and pre-publication boundaries;
- normalized unique relative paths and exact directory/file closure;
- regular-file-only topology with no links, reparse points, aliases, devices,
  sockets, mount escapes, or multiply-linked destinations;
- stable package root/directory/file identity, size, modification, bytes, and
  final topology;
- stable target root/stage/directory/file identity and topology;
- exact streamed SHA-256 equality for every source and staged file; and
- no modification of the immutable recovery point.

Reverify the complete stage, package, target root identity, and target contents
before publication. The only publication name is the fixed child
`ShowVault Restored Files`. It must be absent. Publish it with one
non-overwriting same-filesystem atomic rename inside the retained selected
target; never replace or delete the selected target directory itself.

Cancellation is honored through the final pre-publication boundary. After the
atomic rename begins, Restore enters a bounded finalization section: it either
reverifies, writes durable evidence, and completes, or removes/quarantines only
the exact newly owned published identity. It never reports a cancelled partial
success.

### 4. Durable path-free Restore evidence and restart repair

Add a transactional, idempotent Restore-attempt table to the existing SQLite
database with closed states such as
`staging → published → verified → completed`, plus `failed` and `cancelled`.
Do not store a target path, target name supplied by the user, credential,
content, or security-scoped grant.

Write a bounded evidence record under `Reports/Restores` only after full
published-tree verification. It binds the recovery-point ID, file/byte counts,
manifest digest, completed time, passed result, and evidence digest. It does
not claim application loading, dependency closure, compatibility, license
transfer, production readiness, or Recovery Confidence.

The stage intent inside the reselected target and path-free vault state support
restart repair:

- incomplete owned staging may be safely discarded and restarted;
- a published child plus matching owned intent is fully reverified before
  evidence completion or exact-owned rollback;
- a completed Restore is idempotently recognized only when target bytes,
  vault evidence, and SQLite state agree; and
- an unknown final child, unknown stage, missing evidence, conflict, or target
  mutation is preserved and surfaced for operator attention rather than
  adopted or overwritten.

### 5. Flutter attended Restore UI

Keep milestone-1 Scan, authentication, milestone-2 Save/Cancel, and vault
reopening intact. Add only:

- Restore on a freshly verified local recovery point;
- a plain warning that Restore copies files into a sandbox and does not load a
  running application/device;
- an independent native picker for an existing empty target sandbox;
- Cancel before publication and bounded path-free progress;
- separate `Restored locally`, `Restore attention`, and cancelled/failed
  states; and
- reselect-and-resume behavior after restart without rescanning the original
  source or requiring login/network.

No upload executor, hosted object store, tenant protocol, automatic Restore,
in-place application data replacement, live-device write, or cloud-required
flow belongs in milestone 3.

### 6. Current documentation and handoff

Add the current attended Restore runbook and update the product bible, vault
guide, roadmap, readiness, root/client READMEs, evidence, and continuation
handoff from actual results. Historical installed macOS proof remains
historical and must not be claimed for the reconstructed branch.

## Required adversarial tests

Use synthetic roots only and cover at minimum:

- target/vault equality, both nesting directions, alias identity, linked target
  root/ancestor/descendant, linked vault/package component, mounted/reparse
  substitution, non-directory target, and non-empty target;
- fixed publication-child conflict and malformed, linked, swapped, incomplete,
  duplicate, oversized, or unowned intent/staging trees;
- package root/directory/file swaps before and during copy, changed bytes with
  stable size/time, late/missing/extra entries, unsafe/duplicate paths, and
  manifest/evidence mismatch;
- target root/stage/directory/file swaps, late target entry, destination hard
  link, unsupported entry, and topology mutation before/during publication;
- empty package and every directory/file/path/per-file/aggregate/time bound;
- cancellation during package reverify, enumeration, copy, stage verification,
  pre-publication, and durable evidence transition;
- existing identical completed Restore, conflicting child, repeated Restore,
  SQLite failure, evidence failure, and restart at each durable state;
- exact cleanup of owned stage/published identities and preservation of every
  unrelated or ambiguous operator entry;
- unchanged immutable package bytes after success, cancellation, and every
  failure class;
- no source/vault/target path in result, evidence, SQLite, logs, UI errors, or
  cloud-facing API payloads;
- signed-out/offline Restore and restart reselect/resume without source rescan;
  and
- macOS/Windows behavior behind the same local-engine contract, with honest
  skips where native reparse/mount proof is unavailable.

## Verification gate

Run all milestone-2 regressions plus focused/full local-engine and Flutter
tests, changed .NET Release builds and format checks, Flutter analysis/format,
generated-plugin/project/shell checks, EF/API regressions, and
`git diff --check`. Perform an end-to-end synthetic packaged-host Restore and
prove that its protocol/output/evidence/database contain no fixture paths.

Inspect the complete diff for pathname reopening, target/vault overlap,
unretained identities, broad deletion, selected-target replacement,
overwrite/in-place Restore, publication before full stage verification,
success before durable evidence, cleanup of unowned content, package mutation,
cloud/login dependency, arbitrary host operations, Agent customer-flow
exposure, personal-data access, and native-proof overclaims.

Passing tests do not authorize push, PR mutation, workflow dispatch, artifact
retrieval, native packaging/installation, equipment, personal/customer/venue
data, cloud resources, upload/hosted synchronization, release, deployment, or
destructive cleanup.
