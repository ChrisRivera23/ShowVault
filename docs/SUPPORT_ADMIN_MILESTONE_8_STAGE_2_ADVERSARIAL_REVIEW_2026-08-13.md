# Support Admin milestone 8 stage 2 adversarial review — 2026-08-13

## Verdict

Verdict: **approve after three bounded repairs; stop before stage 3.**

The exact stage-2 implementation was reviewed against the repaired plan,
reviewed stage-1 authority, implementation evidence, full Git delta, route and
scheme inventory, transaction/query behavior, response contracts, and repeated
validation. Three actionable fail-closed defects were repaired with focused
regression coverage. No unresolved stage-2 trust, privacy, authorization,
audit, cardinality, or scope blocker remains.

## Exact review input

- Reviewed stage-1 parent:
  `fc59c2658f977be572bbfdf4123baf3ca72bd9ca`.
- Stage-2 implementation commit:
  `990d384e8b6b443d121b3cf83fa6fca182d9a732`.
- Stage-2 implementation tree:
  `ba2838866116b43e9c4f9837cf6d3d97b4488ffc`.
- Stage-2 delta: 11 files, `+802/-1`.
- Sorted path-list SHA-256:
  `3168d6fb6cb931bedc3a10d1f137fb8c9e4c5aca35d05a2bc3e728c7150c7766`.
- Binary full-index diff SHA-256:
  `e18cb1ae3257b4f24bdff028eb83670e5a495c2e2ef89cd35bfc6747df9e3dee`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

The worktree was clean and every input pin matched before review repairs.

## Findings and repairs

### 1. Concurrent limiter partition creation could exceed the hard cap — repaired

The implementation checked the concurrent dictionary count and inserted a new
partition without an atomic creation boundary. Different subjects arriving at
the same time could each observe capacity below 4,096 and collectively exceed
the frozen cap.

New-partition lookup, pruning, capacity recheck, and insertion now share a
dedicated lock while existing-partition request accounting retains its
per-entry lock. The dictionary remains bounded under concurrent new-subject
pressure. Regression coverage seeds 4,032 partitions, races 128 distinct new
partitions, proves exactly 64 succeed, and proves the final count is exactly
4,096.

### 2. Support challenge and forbid responses could leave the Support boundary — repaired

Authorization middleware can challenge before the endpoint handler sets its
headers, so unauthenticated Support responses lacked the route's mandatory
`Cache-Control: no-store`. Separately, handler-side `Results.Forbid()` without
an explicit scheme could invoke the API's default `ShowVault-User` policy
scheme, whose forwarding selector includes personal-beta authentication. This
did not grant Support access, but it violated scheme isolation and uniform
non-cacheable response handling.

The dedicated Support JWT events now apply `no-store` during message receipt,
challenge, and forbid. Handler-side claim/staff denials return an empty direct
403 rather than invoking any default authentication scheme. An enabled-route
integration regression proves an unauthenticated Support POST returns 401 with
`no-store`; source inventory proves the endpoint authorizes only
`ShowVault-Support` and has no handler-side `Results.Forbid` call.

### 3. Enabled authority bounds could exceed the persisted identity invariant — repaired

An HTTPS origin longer than 255 characters could pass startup even though the
stage-1 assignment and audit issuer columns, and their domain invariant, are
limited to 255. Such enabled configuration could never resolve a matching
staff assignment.

Startup validation now rejects canonical authorities over 255 characters.
Audience validation also rejects control characters and compares against the
trimmed customer audience. Focused tests cover overlong authority,
control-character audience, unsafe authority, incomplete configuration, and
customer-audience collision.

## Boundaries revalidated

- Disabled checked-in configuration maps no Support route; valid enabled
  configuration maps exactly one POST route with only the Support scheme.
- Exact issuer, stable subject, exact scope/MFA, integer token age, direct-peer
  partitioning, and rate-before-database ordering remain intact.
- Active assignment resolution occurs inside the serializable transaction;
  active grant plus organization are resolved in one joined query.
- Unknown and ungranted targets retain the same result, reason, query count,
  null-organization audit, and response path.
- Success and attributable target denial append immutable audit before commit
  and response; failure or cancellation returns no overview.
- The projection remains the exact closed aggregate shape with bounded source
  cardinality, checked counts/usage, UTC timestamps, exact hosted-sync states,
  and no banned identity/provider/payment/path/content/correlation fields.
- Customer authentication, membership, routes, and personal-beta behavior are
  unchanged; the repair adds no BFF, migration, provisioning, provider, or
  native behavior.

## Repeated validation

- Platform suite: **40 passed, 0 failed, 0 skipped**.
- API suite: **170 passed, 0 failed, 0 skipped**.
- API Release build: **0 warnings, 0 errors**.
- API source/test formatting verification: **passed**.
- EF pending-model gate: **no pending model changes**.
- Exact input pins, diff whitespace, route/scheme inventory, and secret-pattern
  scan: **passed**.

All review fixtures are synthetic. No GitHub, workflow, provider, production,
deployment, release, native, or cleanup mutation occurred.

## Stop boundary and next gate

Stage 2 is approved after the three repairs. Stop here. The next task requires
fresh explicit authorization and should first perform a read-only review of the
exact repair commit and evidence. Repaired plan stage 3—the separate disabled
Support BFF—must not begin until that review is complete and separately
authorized.

No authorization here extends to stage 3, staff provisioning, customer-route
changes, GitHub mutation, fetch/push, workflow dispatch/rerun, cleanup, release,
deployment, identity/provider/production operations, Keychain-value access,
real-person/customer/venue data, or native operations.
