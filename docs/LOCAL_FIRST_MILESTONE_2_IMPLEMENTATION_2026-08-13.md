# Local-first milestone 2 implementation evidence — 2026-08-13

## Boundary

This local-only implementation starts at exact authorized planning foundation
`5c881f1910a40c989a8dd96afec4cbb054751e92` on branch
`codex/local-first-milestone-2` in isolated worktree
`/private/tmp/showvault-local-first-m2/worktree`.

No fetch, push, PR or workflow mutation, artifact retrieval, native package
installation, signing, equipment access, personal/customer/venue data, cloud
resource, upload, restore, release, deployment, or destructive repository
cleanup occurred.

## Reviewable implementation commits

1. `a127db3` — venue-neutral .NET local engine, stable bounded capture,
   deterministic manifest/evidence, canonical vault, transactional SQLite
   queue, closed host, and adversarial tests.
2. `39d4915` — native folder consent, signed-out/offline Save/Cancel UI,
   path-free progress/status, restart vault opening, desktop host packaging
   configuration, generated file-selector registrations, and Flutter tests.
3. `19d4688` — cross-platform in-band Cancel record for the packaged host.
4. `a632357` — exact semantic revalidation of durable verification evidence and
   complete count/size/path/duration bounds.
5. `04b6068` — current product bible, local Save/vault guide, readiness, root,
   and client documentation.
6. `3a0492d` — bounded restart repair for `staging` and `verified` states plus
   reverify-or-quarantine handling for interrupted and orphan packages.

Implementation head `3a0492d5b6a0cb2fa379efd62c7abf51fc677865`
has tree `302943f67e3f95f4e21fdb7a0ac4e27ab550d7e1`, 6 commits,
35 changed paths, `+3,232/-59`, binary-diff SHA-256
`38002cd3967a1b5685c2d4d3d7c2d6d4f42c4cee97a9d4e97967299963c63b21`,
and sorted path-list SHA-256
`13095692314fd3b70c03f5b6394487e0c578e246b997218f5d810059c286869b`.

## Behavior proven in source and synthetic tests

- Only a closed-catalog `UserDataRoot` detection can Save; installed
  applications remain detection-only. The selected source must be the exact
  retained identity of the current catalog candidate.
- Source and vault equality, both nesting directions, linked roots and
  descendants, linked vault components, multiply-linked files, non-regular
  entries, cross-volume mounted descendants, unsafe paths, and unstable
  identities/topology/content fail closed.
- Capture enforces directory, file, relative-path, per-file, aggregate-byte,
  duration, and recovery-point bounds with cancellation checks through
  enumeration, copy, verification, publication, and queue transition.
- Same-filesystem staging is verified before non-overwriting atomic package
  publication. A prior recovery point is never overwritten by a repeat or
  failed Save.
- Package and independent manifests/evidence must match byte-for-byte. Restart
  inspection reparses the evidence, streams and rehashes the full content, and
  recomputes the evidence record before returning `Verified locally`.
- SQLite schema creation is transactional and idempotent with foreign keys,
  WAL, `synchronous=FULL`, and a bounded busy timeout. Only
  `staging → verified → queued` can produce `Cloud queued`; records contain a
  vault-relative package identity and no source/vault path.
- Restart repair marks interrupted staging failed, reverifies and queues a
  complete verified state, quarantines a failed verified state, and quarantines
  untracked packages. Queue attention remains distinct from success.
- The host accepts only Save, Cancel, and inspect records over standard
  input/output, emits path-free codes/results, and exposes no network,
  arbitrary command, plugin, Agent identity, enrollment, or service lifecycle.
- Flutter preserves milestone-1 Scan and Auth0 behavior while adding explicit
  source/vault consent, confirmation, bounded progress, Cancel, separate local
  and cloud states, Queue attention, and source-independent vault reopening.

## Validation

- Local-engine Release tests: 41 passed.
- Local-engine host Release build: zero warnings and zero errors.
- Synthetic packaged-host Save and inspect process contract: passed; output
  contained no synthetic source or vault path.
- Flutter analysis: clean.
- Complete Flutter suite: 25 passed.
- Agent contracts: 22 passed.
- Platform: 15 passed.
- Agent: 291 passed.
- API: 19 passed.
- EF `migrations has-pending-model-changes`: no changes.
- Agent and API Release builds: zero warnings and zero errors.
- Changed-project .NET format verification and Dart format check: passed.
- `flutter pub get` reproduced the committed plugin registrants without drift.
- macOS project/entitlement plist checks and both shell syntax checks: passed.
- Packaging negative guards: relative output exited 73; non-loopback no-login
  origin exited 64 before any build.
- `git diff --check` and path/secret literal review: passed.

An initial parallel Release invocation collided on shared MSBuild output files;
the projects were rerun sequentially and passed. The same invocation exposed a
host switch-scope compile error, which was fixed in `19d4688` before the final
Release build and process-contract proof.

## Evidence limits

Automated filesystem tests use synthetic roots only. Platform-specific mount
and reparse behavior is implemented behind the shared contract, but no
privileged mount fixture or Windows host was available for native proof.

No macOS or Windows Flutter application was built, signed, sandbox-tested,
notarized, installed, launched, or upgraded. No Gatekeeper, installed helper,
Windows installer/protocol, personal-Keychain, real application data, venue
equipment, upload executor, restore UI, or end-to-end Auth0 proof is claimed.
