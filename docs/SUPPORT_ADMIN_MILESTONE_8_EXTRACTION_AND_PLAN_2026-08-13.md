# Support Admin milestone 8 extraction and implementation plan — 2026-08-13

## Decision and exact local base

- Product-tree base: `2dfb4cd82b6ca3cf1ef3928f73c8fe00e194b0a5`.
- Product tree: `fea87b4dc7492a5187dcd60cc618ddff77b067db`.
- Remote-main merge with that exact tree:
  `577bbba00206f9e60a2e3c70d759a34af591106a`.
- Branch: `codex/milestone-8-support-admin-plan`.
- Worktree: `/private/tmp/showvault-milestone-8-support-admin-plan`.

The remote-main merge object is not present in the local object database. This
local-only planning branch therefore starts at the exact reviewed PR #39 product
head, whose tree is byte-identical to current remote `main`. No fetch, remote
mutation, source implementation, provider operation, or cleanup was performed.

The first safe staff-support slice is:

**A strongly authenticated active SupportReader with an explicit grant for one
organization enters that organization's opaque ID in a separate ShowVault
Support site, receives one minimized read-only operational overview, and
creates append-only support-access evidence.**

There is no organization directory or fuzzy search, customer impersonation,
write action, provider lookup, secret access, or backup-content access in this
slice.

## Authority reconciliation

`docs/LOCAL_FIRST_PRODUCT_BIBLE.md` remains the customer-product authority.
`docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md` separates desktop, customer
account portal, and internal ShowVault Admin into distinct trust domains.

The former first two roadmap `Next` entries are complete:

1. controlled Stripe sandbox account provisioning and operational lifecycle
   proof completed through PR #39; and
2. the ShowVault-owned customer account portal and membership lifecycle
   completed through milestone 7 and PR #38.

`docs/ROADMAP.md` is reconciled in this planning commit so separately gated
internal ShowVault support administration is the next objective. Production
hosted storage and native/equipment proof remain later independent gates.

## Existing reusable foundation

The repository already provides these bounded building blocks:

- `MembershipAuthorizationService` resolves the authenticated subject against
  current active organization membership and server-owned role. This remains
  customer authority only; it must not grant staff access.
- `MembershipStepUpAuthorization` proves the useful fail-closed pattern of an
  exact scope, exact MFA method, bounded token age, future-skew rejection,
  injected time, and personal-beta denial. Its `manage:members` contract is
  customer-specific and must not be reused as staff authorization.
- `Membership` has closed role/state values and optimistic revisions.
- `AccountAuditEvent` and `CommercialAuditEvent` are append-only through
  `PlatformDbContext.SaveChangesAsync` and already carry bounded action,
  outcome, reason, correlation, policy, organization, actor, and time fields.
  Their stored actor subjects and target IDs are server evidence, not safe
  support response fields.
- Account administration exposes current member/invitation state without
  identity subjects in its customer contract.
- Commercial state already derives normalized entitlement and logical usage;
  billing exposes bounded attention reasons separately from provider bindings,
  event receipts, raw payloads, or payment data.
- The account portal demonstrates a disabled-by-default server-rendered .NET
  BFF, Authorization Code + PKCE, Secure/HttpOnly host-only cookies,
  antiforgery, exact-origin enforcement, server-side tokens, bounded sessions,
  generic failures, restrictive browser headers, and removed HTTP logging.

These are patterns and query sources, not an existing staff trust boundary.

### Reviewed source inventory

The extraction reviewed these exact implementation seams:

- human tenant authority: `Authorization/MembershipAuthorizationService.cs`,
  `Organizations/Membership.cs`, and the tenant/account/billing/commercial/
  hosted-sync endpoint consumers;
- customer step-up: `Security/MembershipStepUpAuthorization.cs`;
- account lifecycle and minimized contracts: `Account/AccountAdministrationService.cs`,
  `Endpoints/AccountEndpoints.cs`, and `Contracts/AccountContracts.cs`;
- append-only evidence and persistence: `Organizations/AccountAuditEvent.cs`,
  `Commercial/CommercialModels.cs`, and `Data/PlatformDbContext.cs`;
- normalized operational state: `Commercial/CommercialStateService.cs`,
  `Billing/BillingService.cs`, `Billing/BillingModels.cs`,
  `Endpoints/CommercialEndpoints.cs`, and `Endpoints/BillingEndpoints.cs`; and
- BFF security pattern: account-portal `Program.cs`, origin/security-header
  middleware, server-side ticket store, typed client, and portal security tests.

Existing API, platform, account administration/adversarial, membership,
commercial, billing, hosted-sync, and portal-security tests provide regression
anchors. No current file defines Support authority or a Support response.

## Gaps that must fail closed

The repository has no staff identity application/audience, staff assignment,
staff role model, organization support grant, staff authorization handler,
support-access audit, Support API contract, or Support website. The existing
customer JWT audience and account-portal cookie are insufficient.

Current audits contain server-known identity subjects; membership records can
contain organization-visible labels; billing tables contain provider IDs;
recovery records can lead toward paths, filenames, manifests, and content.
Direct entity serialization or a general database browser would violate the
privacy boundary. A dedicated projection is required.

The in-memory account-portal ticket store is Development-only. A production
Support site needs a reviewed durable/distributed encrypted server-side session
store and explicit retention/revocation behavior. Local implementation must
remain disabled and synthetic until that operational gate exists.

## Frozen trust and authorization boundary

### Separate identity plane

Add a dedicated JWT bearer scheme, `ShowVault-Support`, with a distinct
configured audience. It must never forward to personal-beta authentication and
must not accept the customer API audience as staff proof. Checked-in Support
configuration remains disabled with null authority/audience. API enablement
requires an exact HTTPS authority and non-empty audience distinct from the
customer API; incomplete enabled API configuration fails startup. BFF
enablement independently requires its complete origin/OIDC/API/session
settings or fails startup. When disabled, the API does not map Support routes
and the BFF serves only its generic `503` disabled response.

The exact local authorization contract is:

- authenticated exact configured issuer plus stable `sub`;
- exact OAuth scope `support:organizations:read`;
- exact MFA evidence in
  `https://showvault.app/authentication_methods`, accepting only exact `mfa`
  string/array values;
- integer `iat` no older than five minutes and no more than 30 seconds in the
  future;
- an active server-owned staff assignment with closed role `SupportReader`;
- an active explicit grant for the exact route organization; and
- no personal-beta, customer membership, route-supplied role, JWT role, portal
  cookie role, email domain, or client state as authority.

The first closed role set contains only `SupportReader`. Later investigator,
billing-support, or administrator roles are not aliases and require separate
policy. One role is intentionally safer than prematurely defining write-capable
staff roles.

### Staff records

Plan three server-only entities:

- `SupportStaffAssignment`: ID, normalized immutable identity issuer and
  subject, `SupportReader`, `Active|Suspended|Revoked`, created/updated times,
  revision;
- `SupportOrganizationGrant`: ID, staff assignment ID, organization ID,
  `Active|Revoked`, created/updated times, revision; and
- `SupportAuditEvent`: ID, nullable organization ID when no safe tenant is
  established, server-known actor subject, action, outcome, bounded reason,
  correlation ID, policy version, and time.

Assignments and grants are provisioned only by synthetic test fixtures in this
milestone. No staff-management endpoint is included. Relationships use
restrictive deletion and unique `(issuer, subject)` and
`(assignment, organization)` invariants. Revoked assignments and grants are
terminal in this milestone. Revisions are concurrency tokens. Support audits
are append-only and cannot cascade-delete.

### Decision order

For every Support request:

1. authenticate only with `ShowVault-Support`, validating the exact configured
   HTTPS issuer and distinct Support audience;
2. resolve the normalized issuer-and-subject pair;
3. validate exact scope, MFA evidence, and token freshness;
4. apply a bounded subject-plus-source rate limit before database resolution;
5. start a serializable database transaction with bounded conflict retry;
6. resolve an active `SupportReader` assignment inside that transaction;
7. resolve the active grant and exact organization in one joined query so
   missing and ungranted targets take the same branch and response;
8. create a minimized projection with bounded `AsNoTracking` queries;
9. append one `support_overview_read` audit, commit the transaction, and only
   then return the projection with `Cache-Control: no-store`.

The rate-limit source is the direct peer unless an independently configured
trusted-proxy boundary supplies it; arbitrary forwarding headers are ignored.
The issuer-subject/source partition is kept server-side, never returned or
logged, and has a fixed expiration/capacity.

Missing, malformed, inactive, wrong-tenant, ungranted, stale, or raced state
denies closed. Unknown, ungranted, and grant-revoked targets share one generic
`support_target_unavailable` response, and no requested organization ID is
written to audit until the joined lookup establishes an active grant. An
attributable denial may record the active staff assignment and uniform reason
with a null organization. Authentication failures that occur before a trusted
staff assignment exists remain bounded security telemetry, not durable Support
audit rows. An audit/commit failure or serialization conflict returns no
overview; inspection without committed evidence is not success.

## Exact first read-only contract

Add one endpoint only:

`POST /api/v1/support/organization-overview`

The strict JSON body contains only `organizationId`, rejects unknown members,
non-JSON content, empty IDs, duplicate members, and bodies over 4 KiB. A POST
query is intentional: the read creates mandatory audit state and keeps the
organization ID out of request paths, query strings, browser history, proxy
access logs, and referrers. The endpoint is never cacheable and accepts no
idempotency or write-action field.

It returns a closed `SupportOrganizationOverview` containing:

- organization ID and bounded display name;
- member counts grouped by closed role and lifecycle state, with no member IDs,
  labels, identity subjects, or invitations;
- internal plan code, normalized license/subscription states, period/grace
  times, eligibility boolean/reason, and committed/reserved/limit bytes;
- open billing-attention count, distinct bounded reason codes, and oldest open
  time;
- hosted-sync session counts mapped only from exact current persisted statuses
  `uploading|completed`, plus latest `UpdatedAt` activity time;
  and
- last account/commercial activity times only, not audit rows or actors.

The organization display name remains bounded to its current 200-character
domain limit. The member matrix contains exactly the five current roles crossed with the
three current lifecycle states (15 non-negative counts in closed enum order).
Billing-attention reason codes are distinct ordinal-sorted closed bounded
strings of at most 80 characters with an exact maximum of eight; more than
eight fails the projection.
Hosted-sync counts contain exactly `uploading` then `completed`. Counts use
checked 64-bit conversion and timestamps are UTC. Unknown database enum/state,
inconsistent usage, excessive result cardinality, arithmetic overflow, or
projection failure returns a generic error and no partial data.

The response excludes staff/customer identity subjects, membership and target
IDs, invitation labels/codes/digests, email/name claims, provider customer/
subscription/session/event/object IDs, provider environment/revisions/raw
payloads, prices, payment methods/cards, credentials/tokens/secrets, IP/user
agent, correlation IDs, filesystem paths, filenames, manifests, backup
contents, restore contents, and signed URLs.

The first site accepts an exact organization GUID in a protected POST form,
calls the API POST server-side, and renders the returned minimized overview
directly in the same `no-store` response. It does not redirect with the GUID or
persist an overview/result handle. API and BFF request-body logging is disabled;
structured logs use only a generated correlation ID and bounded outcome code.
No organization list, search, autocomplete, export, or raw JSON browser is
included.

## Staged implementation plan

Implementation requires fresh authorization and must be split into reviewable
local commits.

### Commit 1 — staff authority, grants, and immutable evidence

Add the three platform entities, exact closed enums/invariants, issuer-subject
and assignment-organization uniqueness, restrictive relationships, EF
mappings, one generated migration, append-only enforcement, disabled Support
options, and domain/persistence tests. Add no endpoint or UI. Synthetic
fixtures are the only assignment/grant provisioning mechanism.

Stop if the migration weakens an existing constraint, adds cascade deletion,
stores email/name/password/token material, or permits a write-capable role.

### Commit 2 — dedicated Support authentication and overview API

Add the separate exact-issuer bearer scheme/audience, frozen step-up evaluator,
subject-plus-source rate limit, `SupportAuthorizationService`, closed request/
response contracts, `SupportOrganizationOverviewService`, joined grant-target
lookup, serializable audited query transaction, and the single strict POST
endpoint. Query only the fields needed for the frozen projection. Record
bounded evidence and require `no-store` responses. Add no customer-route
change.

Stop if customer authentication can satisfy the Support scheme, organization
membership can grant Support access, an ungranted organization is
distinguishable from a missing one, or entity serialization exposes a banned
field. Also stop if a grant can be revoked concurrently while an overview is
returned without a committed audit of the same serializable decision.

### Commit 3 — separate disabled-by-default Support BFF

Add `apps/support_admin` as a server-rendered .NET application with its own OIDC
client, exact origin, host-only cookie names, antiforgery, CSP/referrer/cache
headers, server-side ticket/token storage seam, removed HTTP body/header
logging, generic errors, and an exact-ID POST lookup page that renders without
a redirect or result store. Do not share the account portal cookie, OIDC
client, session store namespace, or pages.

Local Development may use an explicitly synthetic bounded ticket store. Any
non-Development enablement must fail startup until a reviewed durable encrypted
session store is configured. No deployment, Auth0 mutation, DNS/TLS, or real
staff/customer use is part of this commit.

### Commit 4 — complete adversarial evidence and handoff

Run the full API/account-portal/platform suites plus focused Support tests,
Release builds, EF model drift, formatting/diff, secret-pattern, banned-field,
and route-inventory gates. Record exact counts, paths, hashes, and remaining
operational gates in local documentation.

## Required adversarial proof

Implementation is incomplete without tests for:

- absent/malformed subject, wrong audience, customer scheme, personal beta,
  missing/wrong scope, missing/malformed MFA, absent/negative/stale/future
  `iat`, and ordinary customer Owner denial;
- wrong issuer, issuer-subject collision, missing/suspended/revoked staff
  assignment, wrong role, missing/revoked/cross-tenant grant, unknown
  organization, and assignment/grant revocation races;
- no organization enumeration or distinguishable missing/ungranted response,
  including status, body, headers, durable reason, query count, and bounded
  timing class;
- strict POST body/content-type/size/duplicate/unknown-member rejection and no
  organization ID in URL, referrer, access log, structured log, or analytics;
- disabled routes, generic disabled BFF response, incomplete-enabled startup
  failure, exact HTTPS issuer, and distinct-audience configuration;
- exact minimized shape and banned-field absence in success, error, logs,
  audit, HTML, cookies, and snapshots;
- unknown hosted-sync status, inconsistent quota, excessive cardinality,
  canceled request,
  database failure, and audit-write failure returning no partial overview;
- append-only support audit, restrictive deletion, uniqueness, optimistic
  concurrency, serializable retry exhaustion, and one bounded committed event
  per attributable completed decision, whether allow or uniform denial;
- exact-origin, PKCE, nonce/state, no offline access, fresh MFA challenge,
  secure host-only cookie, server-side token, CSRF, session expiry/revocation,
  CSP/frame/referrer/cache protections, and generic portal failures; and
- existing customer membership, commercial, billing, account, hosted-sync, and
  portal authorization remaining unchanged.

All fixtures use synthetic organizations and subjects. No real staff,
customer, venue, email, provider, payment, filesystem, or backup data is
permitted.

## Explicit non-goals and later gates

This plan does not authorize or design customer impersonation, session takeover,
password/MFA reset, organization/member/invitation mutation, refunds,
chargebacks, billing-attention resolution, subscription/license edits, quota
edits, provider dashboard/API access, provider IDs or payment detail display,
backup/path/content inspection, downloads/exports, deletion, retention/legal
hold, staff provisioning UI, global organization search, production identity
configuration, deployment, release, or native operations.

Provider dashboards remain the authority for sensitive payment and identity
operations. Every future Support write action needs an explicit policy,
dedicated role/scope, step-up, concurrency/idempotency design, dual-control
decision where appropriate, immutable audit, adversarial proof, and separate
authorization.

## Next gate

Stop after this documentation-only planning commit. Under fresh authorization,
perform a read-only adversarial review of this exact plan and roadmap diff.
Source implementation begins only after that review finds no unresolved trust,
privacy, authorization, audit, or scope issue and the Product Owner separately
authorizes the staged implementation.
