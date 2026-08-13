# Account portal milestone 7 reconstruction review — 2026-08-13

## Review result

The milestone-7 extraction is approved with the refinements in this document.
There is no historical portal implementation to reconstruct. The safe boundary
is a new server-rendered account BFF plus API/domain changes for email-free
invitations and non-Owner membership lifecycle.

- Exact review base: `08ba3ed2182a302f11ab2f533533fea85e2e57bc`
- Branch: `codex/account-portal-extraction`
- Worktree: `/private/tmp/showvault-account-portal-extraction`
- Historical intent only:
  `ce5be252fee9564b91872b9a6286d52f3f4d9e10`

Product outcome:

**Sign in to the ShowVault account website → select an organization → view
members and invitations → perform MFA step-up → create a seven-day invitation
code for a non-Owner role → a normally authenticated user accepts it once →
the stepped-up Owner changes that member's role or suspends/restores/revokes
access → every ShowVault tenant surface immediately enforces the current active
membership.**

Internal staff Admin, Owner transfer, email delivery, and real-person-data
operations remain separate.

## Reconstruction findings

The current domain has a unique organization/identity-subject membership with
one of five roles, but membership is immutable and has no state or concurrency
revision. Eight API endpoint classes query `Memberships` directly:

1. `TenantEndpoints`
2. `AgentEnrollmentEndpoints`
3. `AgentCommunicationEndpoints`
4. `RecoveryCandidateEndpoints`
5. `RecoveryHistoryEndpoints`
6. `HostedSyncEndpoints`
7. `CommercialEndpoints`
8. `BillingEndpoints`

Adding a `State` column without migrating all eight consumers would create a
security bug: a suspended membership would continue authorizing wherever a
query forgot the state predicate. Centralized active-membership authorization
is therefore part of the first implementation slice, not later cleanup.

The desktop Flutter application is not a portal foundation. It imports
`dart:io`, starts packaged local engines, scans local paths, and has no web
platform. Reusing it for web would couple account authority to code whose
purpose is native recovery. A distinct account surface preserves the existing
desktop boundary.

The guarded personal-beta scheme currently can exercise an existing synthetic
membership and must keep that local Scan behavior, but it must be explicitly
denied organization creation, invitation creation/acceptance, membership
administration, and commercial/provider mutations. Active membership remains
mandatory even in Development.

## Project and trust boundary

Add `apps/account_portal/src/ShowVault.AccountPortal`, a server-rendered ASP.NET
Core Razor Pages BFF. It owns HTML, cookie sessions, OIDC challenge/callback,
antiforgery, and a typed ShowVault API client. It must not reference
`PlatformDbContext`, mutate domain rows directly, or share the private staff
Admin trust boundary.

Use Auth0 Authorization Code flow with PKCE. The portal is a confidential web
client, and tokens remain in a bounded server-side ticket store; the browser
cookie contains only an opaque session handle. Do not use local/session storage,
JavaScript-readable tokens, implicit flow, password grant, or `SaveTokens` in a
self-contained authentication cookie. Microsoft documents the confidential
OIDC/BFF pattern, while Auth0 documents PKCE's code-interception protection.

The checked-in portal must be disabled unless its HTTPS origin, Auth0 issuer,
client ID, audience, callback/logout allowlists, server-side ticket-store
configuration, and client secret source are complete. No secret is checked in.
Production requires a durable encrypted/distributed ticket store and Data
Protection key ring; an in-memory store is Development/test only.

Cookie/security contract:

- `__Host-showvault-account`, Secure, HttpOnly, SameSite=Lax, Path `/`, no
  Domain, 30-minute absolute lifetime, no sliding extension;
- separate Secure/HttpOnly SameSite=Strict antiforgery cookie;
- antiforgery validation on every non-GET form/action;
- exact local return-path allowlist, never an arbitrary return URL;
- `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, CSP with
  `default-src 'self'`, `frame-ancestors 'none'`, and no third-party analytics;
- no invitation code in URLs, rendered HTML after POST, validation echoes,
  telemetry, exception text, or logs; and
- generic error pages with a bounded correlation ID only.

ASP.NET Core automatically supports antiforgery tokens for Razor form POSTs,
but production must explicitly force Secure antiforgery cookies; Microsoft's
current guidance notes the default can otherwise permit insecure cookies.

## Server-owned membership authority

Replace the mutable use of the positional `Membership` record with a domain
entity whose state changes occur only through validated methods:

- `MembershipState.Active`
- `MembershipState.Suspended`
- `MembershipState.Revoked`

Fields:

- `Id`, `OrganizationId`, `IdentitySubject` (server-only), optional bounded
  `DisplayLabel`, `Role`, `State`, `CreatedAt`, `UpdatedAt`, and `Revision`.

Allowed transitions:

| Current | Action | Next | Notes |
|---|---|---|---|
| active | change non-Owner role | active | revision increments |
| suspended | change non-Owner role | suspended | takes effect only after restore |
| active | suspend | suspended | authorization stops immediately |
| suspended | restore | active | current role resumes |
| active/suspended | revoke | revoked | terminal in milestone 7 |

No transition targets an Owner, grants Owner, changes organization/subject, or
leaves `revoked`. Owner transfer and revoked-member re-entry are not inferred.

Add one scoped `MembershipAuthorizationService` as the only human tenant
authority. Its query begins from the authenticated `sub`, organization ID, and
`State == Active`, then applies the required server role and venue ownership.
It returns a bounded authorization result/entity and never accepts client roles.
All eight consumers migrate in the same implementation commit. Organization
listing filters active memberships. Recovery of already-reserved hosted bytes
continues for active, otherwise-authorized members; suspension does not delete
or mutate recovery evidence.

## Invitation state and cryptography

`OrganizationInvitation` fields:

- `Id`, `OrganizationId`, `DisplayLabel` (1–80), target non-Owner `Role`;
- 32-byte HMAC-SHA256 `TokenDigest` and bounded `TokenKeyId`;
- `Pending|Accepted|Revoked|Expired` state;
- creator subject, optional accepted membership ID and accepter subject;
- `CreatedAt`, `UpdatedAt`, `ExpiresAt`, optional terminal timestamp; and
- optimistic `Revision`.

Generate 32 random bytes with the platform CSPRNG and encode base64url without
padding (exactly 43 ASCII characters). Return the raw code once in the 201
response and retain only its HMAC. A separate invitation-token key ring has one
active key plus at most one retiring key. Acceptance computes both candidate
digests, compares fixed-length values, and retains the retiring key until every
invitation signed by it is terminal or expired. Keys never enter Git, database,
logs, portal cookies, tests, or evidence; tests use explicit fixture keys.

Invitation transitions:

| Current | Event | Next |
|---|---|---|
| pending before expiry | accept once | accepted |
| pending | stepped-up Owner revoke | revoked |
| pending at/after expiry | observe/accept/list sweep | expired |
| accepted/revoked/expired | any mutation | unchanged terminal |

Acceptance runs in one database transaction with the membership write and
account audit. A unique digest index plus optimistic revision prevents two
subjects winning the same code. Repeating the accepted code by its winning
subject returns the same membership; all other terminal/unknown codes return
the same bounded `invitation_unavailable` response. An already-active subject
cannot gain or change role via another invitation. Suspended/revoked subjects
cannot bypass their state with a code.

Seven days is the fixed milestone-7 expiry. Creation accepts only Viewer,
Technician, Manager, or Administrator and a bounded organization-visible label.
The label is not asserted to be a legal name or verified identity; real-person
usage remains gated by retention/privacy policy. Member/invitation responses
never expose identity-provider subjects.

## Step-up contract

Ordinary authentication is sufficient for read-only member/invitation lists
and invitation acceptance. Creating/revoking an invitation or changing,
suspending, restoring, or revoking a membership additionally requires:

1. active Owner membership from the database;
2. access-token scope `manage:members`;
3. namespaced access-token claim
   `https://showvault.app/authentication_methods` containing exact value `mfa`;
4. token `iat` no more than five minutes old and not materially in the future
   (30-second clock skew); and
5. normal issuer, audience, signature, and expiry validation.

The portal requests the elevated scope plus Auth0's documented MFA
`acr_values` value. A post-login Action challenges for MFA and a following
Action copies verified authentication-method evidence into the namespaced
access-token claim. Auth0 documents that `amr` must be inspected for exact
`mfa`, that an MFA Action can be triggered using `acr_values`, and that custom
claims can be placed in access tokens.

The portal does not request `offline_access` for elevated authority and does not
refresh an elevated token. The API's five-minute `iat` test limits replay even
if a portal session remains valid. Missing scope/claim/time evidence denies
closed. Local tests use signed synthetic principals only; no Auth0 tenant or
Action mutation belongs to implementation. Exact operational Action code,
client registration, MFA factors, callback URLs, and tenant proof remain a
separate deployment gate.

## Persistence and migration

One migration must:

1. add nullable `display_label`, required `state`, `updated_at`, and `revision`
   to `memberships`;
2. backfill existing rows as active with `updated_at = created_at` and revision
   1 before making required constraints final;
3. retain the unique `(organization_id, identity_subject)` index;
4. create `organization_invitations` with unique 32-byte token digest,
   organization/state/expiry indexes, bounded strings, concurrency revision,
   and restrictive organization relationship;
5. create append-only `account_audit_events` with organization/action/time and
   target indexes; and
6. update the model snapshot with no unrelated schema drift.

`AccountAuditEvent` contains organization ID, actor subject, target entity type
and opaque ID, action, outcome, bounded reason, correlation ID, policy version,
and timestamp. It excludes labels, invite codes/digests, emails, names, tokens,
claims, IP/user-agent, provider/payment data, paths, and request bodies.
`PlatformDbContext.SaveChangesAsync` rejects modified/deleted account audits as
it already does commercial audits.

Organization deletion is not exposed in milestone 7. The new restrictive
relationships avoid silently erasing invitation/audit history; organization
retention/deletion requires its own policy.

## Frozen API shapes

All endpoints require normal authentication. Organization-scoped lists require
an active Owner but not step-up. Mutations marked sensitive require the complete
step-up contract.

- `GET /api/v1/organizations/{organizationId}/account/members`
- `GET /api/v1/organizations/{organizationId}/account/invitations`
- `POST /api/v1/organizations/{organizationId}/account/invitations` (sensitive)
- `POST /api/v1/organizations/{organizationId}/account/invitations/{id}/revoke`
  (sensitive)
- `POST /api/v1/account/invitations/accept`
- `PATCH /api/v1/organizations/{organizationId}/account/members/{membershipId}`
  (sensitive)

Create invitation accepts only `{ displayLabel, role }`; its response contains
the normal summary plus `invitationCode` once. Accept accepts only
`{ invitationCode }`. Member mutation accepts exactly
`{ action, expectedRevision, role? }`, where `role` is present only for
`change_role`.

Member summary:

`{ id, displayLabel, role, state, isCurrentUser, createdAt, updatedAt, revision }`

Invitation summary:

`{ id, displayLabel, role, state, createdAt, updatedAt, expiresAt, revision }`

Closed lowercase strings are parsed explicitly; unknown JSON fields, enum
values, overlong strings/codes, empty IDs, wrong tenants, client subjects,
Owner roles, and impossible action/role combinations are rejected. Responses
never include identity subjects or step-up claims.

Status semantics:

- 401: missing/invalid authenticated subject;
- 403: outsider, wrong role, personal beta, or absent/stale step-up;
- 404: unknown or cross-tenant member/invitation ID;
- 409: expected-revision or current-state conflict;
- uniform 400 `invitation_unavailable`: unknown/expired/revoked/other-winner
  code;
- 201: invitation created; and
- 200: list, accepted/idempotent acceptance, or successful mutation.

Invitation acceptance is limited to five attempts/minute per authenticated
subject plus a bounded source partition. Account mutations use ten/minute per
subject. Forwarded addresses are trusted only behind explicitly configured
proxies; otherwise use the direct connection address. Rate-limit keys are
memory-only in Development and must not be logged.

## Implementation sequence

Implementation should be reviewable in three commits:

1. **Membership authority and schema** — domain state/revision, migration,
   centralized service, all eight consumer migrations, personal-beta
   organization-creation denial, and exhaustive authorization regression tests.
2. **Invitation and account API** — token key ring, state machine, transactions,
   minimized audit, step-up handler, rate limits, endpoints, and adversarial API
   tests.
3. **Account portal BFF** — disabled-by-default OIDC/cookie/ticket-store config,
   member/invitation pages, paste-only code acceptance, explicit step-up flow,
   typed API client, security headers, antiforgery, and rendered/TestServer
   tests.

No commit may leave suspended memberships authorizing on any existing endpoint.
If that cannot be kept atomic, split schema addition from state activation and
keep the feature disabled until every consumer is migrated.

## Required validation

- Existing Platform, API, Agent, local-engine, contracts, and Flutter suites.
- New domain transition and invalid-state tests.
- All eight membership consumers: active role matrix plus suspended/revoked
  denial and wrong-tenant/venue denial.
- Personal-beta organization/account/commercial denial without breaking its
  guarded local Scan path.
- Invitation randomness shape, raw-code absence, key rotation, expiry,
  revocation, idempotency, concurrent acceptance, other-winner denial,
  active/suspended/revoked existing-subject behavior, and cross-tenant IDs.
- Step-up missing scope, missing/wrong MFA claim, stale/future `iat`, expired
  token, personal beta, non-Owner, and fresh valid Owner cases.
- Audit append-only/minimization and transaction rollback.
- Portal OIDC disabled default, server-side ticket storage, cookie flags,
  antiforgery failure/success, local-return allowlist, CSP/no-store/no-referrer,
  no code/token rendering, and no desktop/native dependency.
- EF pending-model check, Release zero-warning builds, focused .NET formatting,
  Razor/static analysis, `git diff --check`, and secret/personal-data scans.

## Sources reviewed

- [Auth0 web-app step-up authentication](https://dev.auth0.com/docs/secure/multi-factor-authentication/step-up-authentication/configure-step-up-authentication-for-web-apps)
- [Auth0 API step-up authentication](https://dev.auth0.com/docs/secure/multi-factor-authentication/step-up-authentication/configure-step-up-authentication-for-apis)
- [Auth0 forced reauthentication and `auth_time`](https://auth0.com/docs/authenticate/login/max-age-reauthentication)
- [Auth0 Authorization Code with PKCE](https://auth0.com/docs/api/authentication/authorization-code-flow-with-pkce/authorize-with-pkce)
- [ASP.NET Core OIDC/BFF guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication)
- [ASP.NET Core antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)

## Authorization boundary and next gate

This review changes documentation only. It authorizes no product source,
migration, package/dependency installation, Auth0 client/Action/MFA mutation,
real personal data, email/SMS, domain/DNS/TLS, deployment, Stripe/provider
operation, internal staff Admin, Owner transfer, organization deletion,
production object storage, external Git action, native action, or destructive
cleanup.

The next bounded task is a milestone-7 implementation plan/handoff that maps
the three commits to exact files, tests, configuration keys, migration steps,
and stop conditions. Product-source implementation remains separately gated
after that plan.
