# Support Admin milestone 8 stage 2 implementation evidence — 2026-08-13

## Verdict and exact input

Verdict: **repaired plan stage 2 is implemented locally and passes its focused,
regression, build, configuration, privacy, and model gates. Stop before the
Support BFF.**

- Exact reviewed stage-1 input:
  `fc59c2658f977be572bbfdf4123baf3ca72bd9ca`.
- Input tree: `fe85f6f42f1660325ec9f95f0fb52d2ecdc1c561`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

## Implemented boundary

- A conditional `ShowVault-Support` JWT bearer scheme validates the exact
  configured HTTPS issuer and a non-empty Support audience distinct from the
  customer audience. Enabled incomplete/unsafe configuration fails startup;
  disabled checked-in configuration maps no Support route.
- The frozen step-up evaluator requires the Support scheme, exact issuer and
  stable subject, exact `support:organizations:read` scope, exact `mfa` evidence,
  and integer `iat` no older than five minutes or over 30 seconds in the future.
- `SupportAuthorizationService` applies step-up before a bounded in-memory
  issuer-subject/direct-peer limiter. Arbitrary forwarding headers are not
  consumed. The limiter has a one-minute ten-request window, two-minute idle
  retention, and a fail-closed 4,096-partition cap.
- The only new route is strict, non-cacheable
  `POST /api/v1/support/organization-overview`. Its JSON parser accepts exactly
  one D-format non-empty `organizationId`, exact `application/json`, and at
  most 4 KiB; lookalike media types, unknown/duplicate fields, and malformed or
  oversized bodies deny.
- `SupportOrganizationOverviewService` uses a serializable transaction with
  three bounded attempts, resolves the active issuer-bound `SupportReader`
  assignment inside it, and performs one joined active-grant/organization
  lookup. Unknown and ungranted targets share the same result, reason, query
  count, and null-organization audit shape.
- The projection contains the exact 15 role/state member cells, normalized
  commercial state and bounded logical usage, at most eight sorted distinct
  billing-attention reasons, exact `uploading|completed` hosted-sync buckets,
  and activity timestamps only. Aggregate source reads are capped at 10,000
  rows and fail as a whole on unknown state, inconsistent usage, overflow, or
  excess.
- One immutable Support audit is committed before an allowed overview or
  uniform attributable target denial is returned. Cancellation, projection,
  audit, database, and serialization failure return no overview.

During validation, a partition-cap edge was found before commit: a new
partition could be inserted after the limiter had reached its nominal maximum.
The implementation now prunes expired entries and otherwise rejects the new
partition while at 4,096; focused regression coverage fills the complete cap
and proves the overflow partition is denied.

## Focused and regression proof

- Focused stage-2 tests cover fail-closed options, scheme/issuer/scope/MFA/time
  claims, limiter window/partition/cap behavior, disabled and enabled route
  inventory, strict body/media/duplicate/size handling, the fixed minimized
  response shape and banned-field absence, committed allow audit, and uniform
  unknown/ungranted query/audit behavior.
- Platform suite: **40 passed, 0 failed, 0 skipped**.
- API suite: **170 passed, 0 failed, 0 skipped**.
- API Release build: **0 warnings, 0 errors**.
- EF pending-model gate: **no pending model changes**; stage 2 adds no migration.
- API source and test formatting verification: **passed**.
- Diff whitespace, single-route inventory, separate-scheme inventory,
  response banned-field scan, and credential/private-key pattern scan:
  **passed**.

All fixtures use synthetic identities, organizations, and addresses. No real
staff/customer/venue data, provider identifier, payment detail, path, manifest,
backup content, credential, or production configuration was introduced.

## Stop boundary and next gate

Stop after the local stage-2 implementation/evidence commit. The next task
requires fresh authorization and should be a complete adversarial review of the
exact stage-2 source, tests, configuration, route surface, transaction/query
behavior, response shape, and evidence. Repair only stage-2 findings and stop
again before repaired plan stage 3.

No authorization here extends to the Support BFF, staff provisioning, customer
route changes, GitHub mutation, fetch/push, workflow dispatch/rerun, cleanup,
release, deployment, identity/provider/production operations, Keychain-value
access, real-person/customer/venue data, or native operations.
