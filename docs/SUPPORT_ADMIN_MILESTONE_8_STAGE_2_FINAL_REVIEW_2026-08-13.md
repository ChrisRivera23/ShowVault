# Support Admin milestone 8 stage 2 final review — 2026-08-13

## Verdict

Verdict: **approve with no further repair; stage 2 is complete; stop before
stage 3.**

The exact repair-review commit was independently reviewed against its parent,
the repaired milestone plan, and all stage-2 implementation and review
evidence. The residual limiter pruning/accounting race is closed, its focused
coverage is sound, and the complete stage-2 boundary remains regression-free.
No actionable finding remains.

## Exact review input

- Original stage-2 implementation:
  `990d384e8b6b443d121b3cf83fa6fca182d9a732`.
- Prior repair commit:
  `a4106e073d6d0e26040d91d739353896b25034f3`.
- Repair-review commit:
  `90f9a8ac7833b1754e387d32c0255307c658e7c7`.
- Repair-review tree:
  `9d624b27f6336a9fe4cbd7226ecc93a9b61aa42b`.
- Repair-review delta: three files, `+133/-13`.
- Sorted path-list SHA-256:
  `186978e69d026f256d792f6c720edd3fb86aa9a9b881bd8f4a9ee1404593dd2d`.
- Binary full-index diff SHA-256:
  `39daec50fd21e3d5c89d13cc02896a364078fb4449488376e48291942b54ee9f`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

The worktree was clean and the commit, sole parent, tree, branch ref, path
hash, and binary-diff hash all matched before review.

## Limiter repair review

`SupportRequestRateLimiter` now places all mutable partition state behind one
gate: lookup, bounded stale pruning, creation, fixed-window rollover,
`LastSeenAt` update, permit check, and increment. `PartitionCount` uses the
same gate. Consequently, pruning cannot remove a partition while a request is
accounting against that entry, and a same-key request cannot recreate a
detached entry with a reset counter.

The gate admits at most 4,096 entries and pruning scans at most that bounded
set. It performs no I/O or asynchronous work and has no nested entry lock, so
the repair introduces no lock-order cycle. The length-delimited
issuer/subject plus direct-peer key remains unambiguous and server-only.

Focused proof independently passed all seven `SupportStage2Tests`, including:

- ten permits and denial of the eleventh per exact partition;
- issuer-subject/source separation;
- exact sequential and concurrent 4,096-partition capacity;
- pruning after the fixed two-minute retention;
- preservation of the refreshed active entry while stale entries are removed;
  and
- continued enforcement of that active entry's post-window counter.

## Complete boundary review

- Support remains disabled by default and maps exactly one POST route only when
  enabled, authorized solely by `ShowVault-Support`.
- Support JWT message, challenge, and forbid events retain `no-store`; the
  Support handler has no `Results.Forbid()` call or default customer/personal-
  beta forwarding path.
- Exact HTTPS issuer and distinct bounded audience validation remain aligned
  with the persisted 255-character issuer invariant.
- Exact issuer, stable subject, scope, MFA, integer token age, rate-before-
  database order, and direct-peer partitioning remain unchanged.
- The strict 4-KiB JSON POST, one joined grant/organization lookup, uniform
  target denial, serializable audit-before-disclosure transaction, fixed
  minimized projection, bounded cardinality, failure behavior, and banned-
  field exclusions remain intact.
- Source inventory found one Support route declaration, one named Support
  scheme binding, no Support endpoint `Results.Forbid()` call, and no
  forwarded-header use in the Support boundary.
- No customer route, BFF, migration, staff provisioning, provider,
  production, or native behavior was added by the reviewed delta.

## Repeated validation

- Focused Support stage-2 tests: **7 passed, 0 failed, 0 skipped**.
- Platform suite: **40 passed, 0 failed, 0 skipped**.
- API suite: **170 passed, 0 failed, 0 skipped**.
- API Release build: **0 warnings, 0 errors**.
- API source and test formatting verification: **passed**.
- EF pending-model gate: **no pending model changes**.
- Exact Git pins, diff whitespace, route/scheme/forwarded-header inventory,
  and response sensitive-field inventory: **passed**.

All fixtures were synthetic. No GitHub, fetch/push, workflow, provider,
production, deployment, release, native, or cleanup mutation occurred.

## Stop boundary and next gate

Stage 2 is complete and approved. Stop here. Repaired plan stage 3, the
separate disabled-by-default Support BFF, requires fresh explicit authorization
and must begin from this exact documentation-only final-review head. This
review does not authorize stage 3 or any external or operational mutation.
