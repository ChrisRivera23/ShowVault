# Support Admin milestone 8 stage 1 implementation evidence — 2026-08-13

## Verdict and exact input

Verdict: **stage 1 is implemented locally and passes its domain, persistence,
migration, regression, and build gates. Stop before stage 2.**

- Input/repaired-plan commit:
  `095fc8102a020e7db7fb65eccf5a2196a01c018f`.
- Input tree: `a354fd6cf5b646a516c0af1ceda26d8f5b9742e8`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

Only repaired plan stage 1 was implemented. There is no Support authentication,
rate limiting, authorization service, overview request/response, endpoint, BFF,
staff provisioning route/UI, customer-route change, provider access, or real
data in this slice.

## Implemented foundation

### Closed domain authority

`ShowVault.Platform.Support` now contains:

- `SupportStaffAssignment` with immutable normalized HTTPS issuer plus subject,
  the only role `SupportReader`, `Active|Suspended|Revoked` lifecycle, monotonic
  timestamps, optimistic revision, suspend/restore/revoke transitions, and
  terminal revocation;
- `SupportOrganizationGrant` with exact assignment and organization IDs,
  `Active|Revoked` lifecycle, monotonic timestamps, optimistic revision, and
  terminal revocation; and
- immutable `SupportAuditEvent` with optional established organization,
  issuer-bound actor, bounded action/outcome/reason/correlation/policy fields,
  and occurrence time.

Issuer and subject are each limited to 255 characters. The issuer must be an
absolute HTTPS URI without user info, query, or fragment. Bounded strings reject
control characters. The 255/255 issuer-subject bounds keep the composite unique
index within practical PostgreSQL B-tree entry limits, including multibyte text.

### Persistence and migration

`PlatformDbContext` adds the three sets and mappings with:

- database check constraints for the single role and closed states;
- unique `(IdentityIssuer, IdentitySubject)` staff identity;
- unique `(StaffAssignmentId, OrganizationId)` grant;
- concurrency-token revisions;
- restrictive grant-to-assignment, grant-to-organization, and optional
  audit-to-organization foreign keys; and
- support-audit indexes by time and organization/time.

Append-only enforcement now covers account, commercial, and Support audits for
both synchronous and asynchronous EF save paths. The generated migration is
`20260814010158_AddSupportAdministrationFoundation`; inspection confirmed its
`Up` creates only the three new tables and indexes. It alters or drops no
existing table. `Down` drops only those three tables in dependency-safe order.

During regeneration, `dotnet-ef migrations remove` read the configured local
development migration history to confirm the first uncommitted generated
migration was not applied, then replaced only the uncommitted migration files
and snapshot. No database migration was applied or reverted.

### Disabled configuration

Checked-in `SupportAdmin` options are registered with:

- `Enabled: false`;
- `Authority: null`; and
- `Audience: null`.

No runtime behavior consumes those options in stage 1.

## Focused and regression proof

- Platform tests: **40 passed**, including assignment/grant transitions,
  terminal states, stale revisions, reversed time, unsafe issuer/identity
  bounds, and minimized audit bounds.
- API tests: **163 passed**, including five new foundation tests for exact
  persistence, issuer-subject and assignment-organization uniqueness,
  synchronous/asynchronous append-only enforcement, concurrency/restrictive
  model metadata, and disabled checked-in options.
- API Release build: **zero warnings and zero errors**.
- EF `migrations has-pending-model-changes`: **no changes**.
- Generated PostgreSQL migration SQL was inspected: closed checks, restrictive
  foreign keys, required unique indexes, no existing-table mutation.
- Explicit changed-file formatting and `git diff --check`: passed.

All test identities, organizations, correlations, and issuers are visibly
synthetic fixtures. No credentials, tokens, provider IDs, customer/venue data,
filesystem paths, backup content, or production configuration were introduced.

## Stop boundary and next gate

Stop after the local stage-1 implementation/evidence commit. The next task
requires fresh authorization and should be a complete read-only review of the
exact stage-1 source, migration, tests, configuration, diff, and validation
evidence. Repair only stage-1 findings and stop again before stage 2.

No authorization here extends to Support authentication, API/BFF work,
staff/customer/provider operations, GitHub mutation, fetch/push, workflow
dispatch/rerun, cleanup, deployment, release, Keychain-value access,
real-person/customer/venue data, or native operations.
