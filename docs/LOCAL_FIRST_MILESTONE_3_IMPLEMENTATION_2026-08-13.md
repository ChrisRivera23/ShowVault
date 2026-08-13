# Local-first milestone 3 implementation evidence — 2026-08-13

## Result

Milestone 3 is complete locally on `codex/local-first-milestone-3` from exact
planning head `2d6c3d2241b582678a6c475fffd88a3f2fa940a7`.

The source implementation is exact commits:

- `fe7bc52` — packaged local Restore engine, host protocol, durability, and tests;
- `0c44776` — attended Flutter Restore consent/status flow; and
- `88a9c5b` — exact-owned cleanup and ambiguous-content preservation hardening.

The exact source range `2d6c3d2..88a9c5b` contains three commits, 15 paths,
`+1,801/-46`, binary-diff SHA-256
`84de601d736e1cdd3e8163bc7a46f7c6c3f5da875abecf2933aacf7e1c2eab47`,
and sorted path-list SHA-256
`0e9b55eafe62390762e2fd886333562591c4b57e83864b447a1947826060fdce`.

## Delivered outcome

**Open local vault → select a freshly verified recovery point → Restore or
Cancel → verify the fixed sandbox copy → retain path-free local evidence**

- Restore remains signed-out and offline and never contacts the control plane.
- The target is an independently picker-selected existing empty sandbox with a
  retained parent/root identity. Vault equality, nesting, aliases/links, mount
  substitution, non-directory targets, and unexpected entries fail closed.
- Package, independent manifest/evidence, content root, directories, files,
  sizes, hashes, link counts, topology, and resource/time bounds are retained
  and reverified through copy and publication.
- The deterministic hidden intent contains no path. Only the fixed
  `ShowVault Restored Files` child can be atomically published; the selected
  sandbox itself is never deleted or replaced.
- Cancellation is honored through pre-publication. Post-publication work is a
  separately bounded verification/evidence finalization section.
- SQLite persists only path-free
  `staging → published → verified → completed`, failed, or cancelled state.
  `Reports/Restores` evidence binds the manifest and restored counts.
- Reselect repairs an exactly owned interrupted stage, fully reverifies an
  already published child, and recognizes completed bytes/evidence/state
  idempotently. Unknown or late content is preserved as Restore attention.
- The host adds only closed Restore and Cancel records. Flutter owns the native
  warning, target picker, progress, Cancel, Restored locally, and Restore
  attention surfaces.
- The shared retained-file primitive now explicitly applies `0600` through the
  created handle; all 291 Agent tests confirm compatibility.

No Agent identity, configured root, command, queue, credential, service
lifecycle, upload/synchronization, arbitrary output name, network target,
application/device loading, or Recovery Confidence surface was added.

## Adversarial evidence

The 60-test local-engine suite includes synthetic proof for successful and
path-free Restore, exact vault/target overlap, non-empty/linked targets,
unverified/tampered packages, package mutation during copy, destination hard
links, late target entries, selected-target identity swaps, cancellation before
and after publication, exact rollback, ambiguous late-content preservation,
unknown staging, mutated restored bytes/evidence, idempotent reselect, owned
interrupted-stage repair, and the packaged-host JSON process contract.

The synthetic packaged-host proof emitted progress/result records, completed
Restore, and found none of the fixture source, vault, or target paths in host
output. Database and evidence assertions likewise found no selected path.

## Validation

- Local-engine Release tests: 60 passed.
- Synthetic packaged-host Restore process contract: passed and path-free.
- Local-engine host Release build: zero warnings and zero errors.
- Flutter analysis: clean.
- Complete Flutter suite: 26 passed.
- Agent contracts: 22 passed.
- Platform: 15 passed.
- Agent: 291 passed.
- API: 19 passed.
- EF `migrations has-pending-model-changes`: no changes.
- Agent and API Release builds: zero warnings and zero errors.
- Changed-project .NET format and Dart format: clean.
- Locked Flutter dependency/plugin generation: reproduced without repository
  drift.
- macOS/iOS project and entitlement plists plus both shell syntax checks:
  passed.
- Packaging negative guards: relative output exited 73; non-loopback no-login
  origin exited 64 before a native build.
- `git diff --check`, closed-operation, path-field, network/Agent, cleanup, and
  complete changed-path review: passed.

## Evidence limits

All filesystem fixtures are synthetic. No personal, customer, venue, or live
application data was accessed. No Git remote, PR, workflow, cloud resource,
upload, hosted synchronization, release, or deployment was used. Flutter's
standard locked dependency resolution accessed public package tooling but
produced no repository drift.

No macOS or Windows Flutter application was built, signed, sandbox-tested,
notarized, installed, launched, or upgraded. No privileged mount fixture,
Windows reparse host, Gatekeeper, personal-Keychain, protocol activation,
equipment, or live application/device loading proof is claimed.
