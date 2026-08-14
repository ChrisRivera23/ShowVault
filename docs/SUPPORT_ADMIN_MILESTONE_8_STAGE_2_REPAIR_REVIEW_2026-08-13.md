# Support Admin milestone 8 stage 2 repair review — 2026-08-13

## Verdict

Verdict: **approve after one residual limiter repair; stop before stage 3.**

The exact stage-2 repair commit was reviewed against its parent, the repaired
plan, original implementation evidence, and adversarial-review evidence. The
three recorded findings were reproduced from the parent delta. The
configuration and Support-scheme/no-store repairs are complete. The limiter's
atomic creation repair retained one residual prune/accounting race, which is
repaired here with focused coverage. No other residual stage-2 blocker was
found.

## Exact review input

- Original stage-2 implementation:
  `990d384e8b6b443d121b3cf83fa6fca182d9a732`.
- Repair/review commit:
  `a4106e073d6d0e26040d91d739353896b25034f3`.
- Repaired input tree:
  `47e51eaa9ecb2ef35c90d96fa6c690168f0c9833`.
- Repair delta: six files, `+182/-9`.
- Sorted path-list SHA-256:
  `a5dc79897a88c014aae057cca990e26f1d19f194c53d9436cbd8e0a581d599dc`.
- Binary full-index diff SHA-256:
  `a31cbd590cb74c9727f60b0fcb48346fa5df532aeb9a9727ced9bb1bf8a5c383`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

The worktree was clean and all pins matched before this review.

## Reproduction of the three repairs

1. The parent implementation could race past 4,096 because capacity check and
   insertion were separate concurrent operations. The reviewed repair places
   lookup, prune, capacity recheck, and insertion behind one creation gate and
   proves exact capacity under 128 concurrent distinct-partition attempts.
2. The parent Support route could challenge before its handler applied
   `no-store`, and handler `Results.Forbid()` could invoke the default customer
   forwarding scheme. The reviewed repair applies `no-store` in Support JWT
   events, uses only direct empty 403 handler responses, and retains exactly one
   route authorized only by `ShowVault-Support`. The enabled-route integration
   test reproduces a 401 challenge with `no-store`.
3. The parent accepted an enabled authority too long for the 255-character
   persisted issuer invariant. The reviewed repair rejects the canonical
   overlong origin, control-character audience, and trimmed customer-audience
   collision. Valid enabled configuration and disabled route absence remain
   intact.

## Residual finding and repair

### Limiter pruning raced live entry accounting — repaired

Although new partition creation was serialized, existing requests updated
`LastSeenAt` under the entry lock while capacity pruning read and removed that
entry without the entry lock. An in-flight request could therefore race with
pruning, use an entry after removal, and allow a later request to recreate the
same partition with a reset counter.

The limiter now performs lookup, pruning, creation, window rollover,
last-seen update, permit check, and counter increment inside one short global
critical section. `PartitionCount` uses the same gate. The maximum work remains
bounded to 4,096 entries, and no lock-order cycle remains.

Focused coverage now proves:

- ten permits and denial of the eleventh per exact partition;
- issuer-subject/source partition separation;
- exact sequential and concurrent 4,096 capacity;
- stale-entry pruning after the fixed two-minute retention;
- preservation of a refreshed active entry while stale entries are removed;
  and
- preservation of that active entry's post-window request counter.

## Complete boundary revalidation

- Separate scheme/audience, exact issuer, subject/scope/MFA/`iat`, customer and
  personal-beta denial, rate-before-database order, and direct-peer source
  remain unchanged.
- Strict 4-KiB JSON POST parsing, disabled route absence, joined target/grant
  lookup, uniform target denial, serializable audit-before-disclosure, bounded
  projection, failure behavior, and banned-field absence remain intact.
- No customer route, BFF, migration, staff provisioning, provider, production,
  or native behavior was added.

## Repeated validation

- Platform suite: **40 passed, 0 failed, 0 skipped**.
- API suite: **170 passed, 0 failed, 0 skipped**.
- API Release build: **0 warnings, 0 errors**.
- API source/test formatting verification: **passed**.
- EF pending-model gate: **no pending model changes**.
- Exact input pins and diff whitespace: **passed**.

All fixtures are synthetic. No GitHub, workflow, provider, production,
deployment, release, native, or cleanup mutation occurred.

## Stop boundary and next gate

Stage 2 is approved after the residual limiter repair. Stop here. A fresh
authorization should perform one final read-only review of this exact
repair-review commit and evidence before stage 3 is considered. Do not begin
the Support BFF in this gate.
