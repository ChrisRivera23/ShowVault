# Support Admin milestone 8 stage 1 adversarial review — 2026-08-13

## Verdict

Verdict: **approve with no actionable finding; stop before stage 2.**

The complete stage-1 implementation was reviewed against the repaired plan,
its implementation evidence, the exact Git delta, and the generated
PostgreSQL migration SQL. The review found no defect requiring a source,
migration, configuration, or test repair. This review adds documentation only.

## Exact review input

- Repaired-plan parent:
  `095fc8102a020e7db7fb65eccf5a2196a01c018f`.
- Stage-1 implementation commit:
  `8384557fc8ab10f73dedfaae58b51f322f107f46`.
- Stage-1 implementation tree:
  `10a0ea767a3439e5cae07e3a14adfafb79c22c16`.
- Stage-1 delta: 11 files, `+2331/-6`.
- Sorted path-list SHA-256:
  `5f640d6d3ea7360fd0741cce1efa71406226a6614b62691cf4ab654db23a9b08`.
- Binary full-index diff SHA-256:
  `6396566c0c65de1a5fbfa1ce02365fe33c1c47639463aef1c42b229d6f64a27e`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

The worktree was clean at the exact implementation commit before this
documentation-only review evidence was added.

## Adversarial review results

### Domain and identity boundary

- The only staff role is `SupportReader`; assignment state is exactly
  `Active|Suspended|Revoked`, and grant state is exactly `Active|Revoked`.
- Assignment suspension/restoration/revocation and grant revocation require the
  current revision and nondecreasing time. Assignment and grant revocation are
  terminal.
- Staff assignments bind the normalized issuer-subject pair. Issuers are
  bounded absolute HTTPS URIs without user info, query, or fragment; issuer and
  subject are each bounded to 255 non-control characters.
- A suspected malformed-hostless `https:` edge was reproduced independently
  against the runtime. `Uri.TryCreate` rejects the tested hostless forms before
  the domain validator can accept them, so this was not a finding.
- Audit events are immutable domain objects, bind actor issuer plus subject,
  permit a null organization until a safe tenant is established, reject an
  empty organization ID, and bound every string field.

### Persistence and migration boundary

- Database checks close the assignment role/state and grant state sets.
- Unique indexes enforce `(IdentityIssuer, IdentitySubject)` and
  `(StaffAssignmentId, OrganizationId)`.
- Assignment and grant revisions are optimistic concurrency tokens.
- Grant-to-assignment, grant-to-organization, and optional
  audit-to-organization relationships all use restrictive deletion.
- Synchronous and asynchronous tracked saves reject modification or deletion
  of account, commercial, and Support audit rows. The new Support enforcement
  preserves and broadens the preexisting audit behavior.
- EF reports no pending model changes. The independently generated migration
  SQL is 2,999 bytes/59 lines with SHA-256
  `ec5ade21edb81f7b6061a509e4529dde69f62ea360f717a373db7140ea9bc41e`.
  It creates only the three Support tables, their restrictive foreign keys,
  checks, indexes, and migration-history row. It contains no existing-table
  alteration, data update/delete, or destructive `Up` operation.

### Configuration, privacy, and scope boundary

- Checked-in Support configuration remains disabled with null authority and
  audience. Stage 1 only registers options; it does not consume or enable them.
- The stage-1 source delta adds no route, authentication scheme, authorization
  handler, rate limiter, overview contract/service, BFF, provisioning surface,
  customer-route change, provider call, or native operation.
- No credential/private-key pattern was introduced. Matches from the
  generated migration designer for preexisting manifest/provider properties
  are model metadata only; no Support entity or response exposes those fields.
- All new test data is synthetic.

## Repeated validation

- Platform suite: **40 passed, 0 failed, 0 skipped**.
- API suite: **163 passed, 0 failed, 0 skipped**.
- API Release build: **0 warnings, 0 errors**.
- All four affected source/test projects pass
  `dotnet format --verify-no-changes --no-restore`.
- EF pending-model gate: **no pending model changes**.
- Exact commit/tree/parent/path-list/binary-diff pins: **passed**.
- Committed-diff whitespace check, route inventory, secret-pattern scan, and
  migration-operation inventory: **passed**.

## Stop boundary and next gate

Stage 1 is approved without repair. Stop here. Repaired plan stage 2—the
dedicated Support authentication and minimized audited overview API—requires
fresh explicit authorization and a new bounded implementation commit.

No authorization here extends to stage 2, the Support BFF, GitHub mutation,
fetch/push, workflow dispatch/rerun, cleanup, release, deployment,
identity/provider/production operations, Keychain-value access,
real-person/customer/venue data, or native operations.
