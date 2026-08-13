# Account portal milestone 7 implementation plan handoff — 2026-08-13

## Checkpoint and authorized outcome

- Exact planning base: `1bf36c7033af7d993c745ff8100883200b6d5be7`
- Branch: `codex/account-portal-extraction`
- Worktree: `/private/tmp/showvault-account-portal-extraction`
- Extraction: `08ba3ed2182a302f11ab2f533533fea85e2e57bc`
- Architecture review: `1bf36c7033af7d993c745ff8100883200b6d5be7`

Read these completely before implementation:

- `docs/ACCOUNT_PORTAL_MILESTONE_7_EXTRACTION_2026-08-13.md`
- `docs/ACCOUNT_PORTAL_MILESTONE_7_RECONSTRUCTION_REVIEW_2026-08-13.md`
- `docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md`
- `CHAT_CONTINUATION_README.md`

Exact outcome:

**Sign in to a separate ShowVault account website → select an organization →
view members and invitations → complete fresh MFA step-up → create a seven-day
email-free invitation code for a non-Owner role → another normally
authenticated user accepts it exactly once → the stepped-up Owner changes that
member's role or suspends/restores/revokes access → all existing ShowVault
tenant surfaces immediately enforce current active membership.**

This is a local implementation plan. Auth0 tenant configuration, real people,
email, deployment, staff Admin, Owner transfer, and Stripe operations are not
part of the implementation slice.

## Commit 1 — membership authority and complete schema

Purpose: introduce the lifecycle without ever leaving an endpoint that can
authorize a suspended/revoked membership.

### Domain files

Modify:

- `services/platform/src/ShowVault.Platform/Organizations/Membership.cs`

Add:

- `services/platform/src/ShowVault.Platform/Organizations/OrganizationInvitation.cs`
- `services/platform/src/ShowVault.Platform/Organizations/AccountAuditEvent.cs`
- `services/platform/tests/ShowVault.Platform.Tests/AccountLifecycleTests.cs`

`Membership` becomes a domain entity with private mutation, `DisplayLabel`,
`Active|Suspended|Revoked`, `UpdatedAt`, and `Revision`. Creation accepts a
`TimeProvider`-derived timestamp from the caller rather than reading system time
internally. Domain methods implement only the reviewed transitions and reject
Owner targets/roles, empty IDs, stale revisions, and reversed timestamps.

The invitation and audit entities are included now because the reviewed schema
uses one migration. Invitation methods own pending/terminal transitions; audit
is immutable after construction. Platform tests cover every allowed and denied
transition, exact boundary lengths, revision increments, and terminal states.

### Persistence files

Modify:

- `services/api/src/ShowVault.Api/Data/PlatformDbContext.cs`
- `services/api/src/ShowVault.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs`

Generate:

- `services/api/src/ShowVault.Api/Data/Migrations/20260813090000_AddAccountMembershipLifecycle.cs`
- matching `20260813090000_AddAccountMembershipLifecycle.Designer.cs`

The migration must be generated from the model, then manually inspected. It:

1. adds nullable/backfillable membership fields;
2. sets existing `state='Active'`, `updated_at=created_at`, and `revision=1`;
3. applies final required/max-length/concurrency constraints;
4. preserves unique `(organization_id, identity_subject)`;
5. creates invitations with a unique fixed 32-byte digest plus organization,
   state, and expiry indexes;
6. creates account audits and organization/action/time plus target indexes; and
7. uses restrictive new relationships so account evidence is not silently
   cascade-deleted.

`SaveChangesAsync` rejects modified/deleted `AccountAuditEvent` rows. Configure
membership and invitation revisions as concurrency tokens. Use exact closed
string enum conversions and database check constraints for state and non-Owner
invitation roles.

### Central authorization files

Add:

- `services/api/src/ShowVault.Api/Authorization/MembershipAuthorizationService.cs`
- `services/api/tests/ShowVault.Api.Tests/MembershipAuthorizationTests.cs`

Modify all eight direct consumers in the same commit:

- `Endpoints/TenantEndpoints.cs`
- `Endpoints/AgentEnrollmentEndpoints.cs`
- `Endpoints/AgentCommunicationEndpoints.cs`
- `Endpoints/RecoveryCandidateEndpoints.cs`
- `Endpoints/RecoveryHistoryEndpoints.cs`
- `Endpoints/HostedSyncEndpoints.cs`
- `Endpoints/CommercialEndpoints.cs`
- `Endpoints/BillingEndpoints.cs`

Register the scoped service in `Program.cs`. Every human tenant decision starts
from authenticated `sub`, exact organization, `State == Active`, then server
role and exact venue ownership where relevant. Organization listing filters
active memberships. Route IDs, JWT roles, portal roles, and client state never
grant authority.

Add one shared helper for detecting the guarded personal-beta authentication
type. `CreateOrganizationAsync` rejects it before creating any organization,
membership, commercial record, or audit. Existing local-beta Scan continues to
work only through its current pre-seeded active synthetic membership and role
checks.

`MembershipAuthorizationTests` must exercise every consumer using active,
suspended, and revoked fixtures. It can call public endpoints rather than
private helpers and must include wrong organization/venue, missing subject, and
role boundaries. Existing endpoint-specific tests stay in place.

Commit stop condition: do not commit if `rg "Memberships"` finds an
authorization query outside `MembershipAuthorizationService`, account
administration persistence, test seeding, DbContext configuration, or migration
code. Explicitly classify each remaining occurrence.

## Commit 2 — invitation and membership administration API

### Configuration and cryptography

Add:

- `services/api/src/ShowVault.Api/Account/AccountInvitationOptions.cs`
- `services/api/src/ShowVault.Api/Account/InvitationTokenService.cs`
- `services/api/src/ShowVault.Api/Account/AccountAdministrationService.cs`
- `services/api/src/ShowVault.Api/Security/MembershipStepUpAuthorization.cs`

Modify:

- `services/api/src/ShowVault.Api/Program.cs`
- `services/api/src/ShowVault.Api/appsettings.json`

Checked-in configuration:

```json
"AccountInvitations": {
  "Enabled": false,
  "LifetimeHours": 168,
  "ActiveKeyId": null,
  "Keys": [],
  "MaximumCodeBytes": 64
}
```

Runtime secret mapping uses:

- `AccountInvitations__Enabled`
- `AccountInvitations__ActiveKeyId`
- `AccountInvitations__Keys__0__Id`
- `AccountInvitations__Keys__0__SecretBase64`
- optional index `1` only during rotation

Require one active key, at most two total keys, distinct bounded IDs, and
base64-decoded 32-byte secrets. Only `Enabled=true` plus a complete valid key
ring enables creation/acceptance. Listing and membership mutations remain
available without invitation keys. Never accept an inline secret from an API
request.

`InvitationTokenService` uses `RandomNumberGenerator.GetBytes(32)`, base64url
without padding, exact 43-character input, HMAC-SHA256, both rotation keys,
fixed digest sizes, and no token logging. Fixture keys/codes contain explicit
`fixture` labels. The service exposes no raw token after the creation response.

### Step-up authorization

`MembershipStepUpAuthorization.cs` contains constants, requirement, handler,
and result reasons for:

- exact scope `manage:members`;
- exact claim `https://showvault.app/authentication_methods` with value `mfa`;
- maximum access-token age 300 seconds;
- future clock skew 30 seconds; and
- personal-beta rejection.

Parse the space-delimited OAuth `scope` claim exactly. Parse `iat` as a
non-negative invariant integer. The handler uses injected `TimeProvider`, never
client time. It supplements, but never replaces, active Owner lookup in the
service. No configuration setting may weaken the frozen scope/claim/freshness
values.

### API files

Add:

- `services/api/src/ShowVault.Api/Contracts/AccountContracts.cs`
- `services/api/src/ShowVault.Api/Endpoints/AccountEndpoints.cs`
- `services/api/tests/ShowVault.Api.Tests/InvitationTokenServiceTests.cs`
- `services/api/tests/ShowVault.Api.Tests/AccountAdministrationTests.cs`
- `services/api/tests/ShowVault.Api.Tests/MembershipStepUpTests.cs`

Modify:

- `services/api/tests/ShowVault.Api.Tests/TenantApiFactory.cs`

Map the six reviewed routes from the architecture review. Parse closed action,
role, state, and JSON shapes explicitly; reject unknown fields using endpoint
JSON options or request-level validation. The service, not endpoints, owns
transactions and state transitions.

Account mutation transaction order:

1. resolve authenticated subject and active Owner;
2. validate fresh step-up for sensitive operations;
3. load exact-tenant target and expected revision;
4. apply domain transition;
5. append minimized account audit; and
6. save once, mapping concurrency failure to 409.

Invitation acceptance:

1. reject personal beta and malformed code before database work;
2. compute candidate digests for the bounded key ring;
3. load a single invitation without exposing validity in error text;
4. atomically expire or accept under revision concurrency;
5. reject active/suspended/revoked existing membership as reviewed, except an
   accepted-code replay by its exact winning subject returns the same row;
6. create active non-Owner membership with inherited display label/role;
7. append audit; and
8. save once.

Register `account-invitation-accept` (five/minute) and
`account-administration` (ten/minute) endpoint rate-limit policies partitioned
by authenticated subject plus direct/trusted-proxy source. Never log the
partition key.

Extend `TestAuthenticationHandler` with explicit fixture-only headers for
scope, MFA method, and `iat`; tests must opt in rather than receiving elevated
claims by default. Configure a deterministic fixture invitation key through
test options.

API tests cover the full matrix frozen in the architecture review, including
concurrent acceptance and mutation, uniform unavailable-code behavior,
one-time token readback, raw-token database/log absence, rotation, expiry,
cross-tenant targets, all roles/states, stale/future step-up, audit rollback,
append-only enforcement, and rate-limit rejection.

Commit stop conditions:

- no invitation code/digest, identity subject, access token, or MFA claim in
  response DTOs, logs, audit details, or exception text;
- no account mutation performs more than one successful `SaveChangesAsync`;
- no Owner target/role path exists; and
- checked-in `AccountInvitations.Enabled` remains false with empty keys.

## Commit 3 — separate account portal BFF

### New projects

Add:

- `apps/account_portal/src/ShowVault.AccountPortal/ShowVault.AccountPortal.csproj`
- `apps/account_portal/tests/ShowVault.AccountPortal.Tests/ShowVault.AccountPortal.Tests.csproj`

Target `net10.0`, enable nullable/implicit usings, and treat warnings as errors.
The only new runtime package is
`Microsoft.AspNetCore.Authentication.OpenIdConnect` `10.0.10`; test packages
match the API test baseline (`Microsoft.AspNetCore.Mvc.Testing` `10.0.10`,
current xUnit/Test SDK). Do not add an Auth0 SDK, JavaScript framework, Node
toolchain, analytics library, or reference to the desktop Flutter app.

### Portal source map

Add:

- `Program.cs`
- `Configuration/AccountPortalOptions.cs`
- `Security/ServerSideTicketStore.cs`
- `Security/PortalSecurityHeadersMiddleware.cs`
- `Clients/ShowVaultAccountClient.cs`
- `Clients/AccountApiModels.cs`
- `Pages/Index.cshtml` and page model
- `Pages/Organizations/Select.cshtml` and page model
- `Pages/Organizations/Members.cshtml` and page model
- `Pages/Invitations/Accept.cshtml` and page model
- `Pages/StepUp.cshtml` and page model
- `Pages/Shared/_Layout.cshtml`
- `wwwroot/css/site.css`
- `appsettings.json`

The typed client accepts only the exact API base origin and account routes. It
sets bearer tokens server-side, uses bounded timeouts/responses, removes default
HTTP client loggers, maps only closed DTOs, and never logs bodies or headers.
Mutation forms use POST (the BFF may translate to API PATCH), antiforgery, and
Post/Redirect/Get so codes and server errors are not re-rendered.

Portal pages show organization name, members, pending/terminal invitations,
role/state, and bounded action controls only. Creation displays the new code on
one no-store page once with an explicit copy control that does not use analytics
or persist locally. Acceptance is a paste-only form with `autocomplete=off` and
clears the submitted value on every result.

### Portal configuration

Checked-in `appsettings.json` remains disabled and secret-free:

```json
"AccountPortal": {
  "Enabled": false,
  "Origin": null,
  "ApiBaseUri": null,
  "Auth0Authority": null,
  "Auth0Audience": null,
  "Auth0ClientId": null,
  "Auth0ClientSecret": null,
  "SessionLifetimeMinutes": 30,
  "ApiTimeoutSeconds": 15,
  "MaximumApiResponseBytes": 1048576
}
```

All origins/base URIs must be absolute HTTPS roots with no user info, query,
fragment, or unexpected path. Client secret comes only from an encrypted runtime
secret source. Enabled non-Development startup fails if configuration,
server-side ticket storage, or persistent Data Protection keys are incomplete.
Development/test may use explicit in-memory fixtures.

OIDC uses Authorization Code + PKCE, exact issuer/audience/callback/logout
paths, state/nonce validation, no offline access, and server-side token tickets.
The normal token is never placed in a self-contained cookie. Step-up requests
`manage:members` plus the reviewed MFA `acr_values`, replaces the server-side
token only after successful callback validation, and expires elevated authority
after five minutes without refresh.

### Portal tests and CI

Add tests for disabled/incomplete startup, synthetic sign-in, server-side ticket
cookie opacity, Secure/HttpOnly/SameSite flags, antiforgery, redirect allowlist,
security headers, no-store pages, member rendering without subjects, raw-code
one-time display/clearing, step-up challenge parameters, API error mapping, and
no token/code/body logging.

Modify `.github/workflows/ci.yml` API job to restore, test, and Release-build
the portal project/tests. No deployment workflow is added.

Commit stop conditions:

- `rg "dart:io|ShowVault.LocalEngine|ShowVault.SyncEngine" apps/account_portal`
  returns no source dependency;
- authentication/access tokens do not appear in browser-readable storage or a
  self-contained cookie;
- every mutation fails without antiforgery;
- disabled/incomplete production configuration cannot serve account pages; and
- no Auth0 tenant, domain, DNS, TLS, deployment, or real user is touched.

## Whole-slice verification order

Run from a clean implementation worktree:

1. `dotnet test services/platform/tests/ShowVault.Platform.Tests/ShowVault.Platform.Tests.csproj`
2. build both local-engine Release hosts, then run all 67 local-engine tests;
3. API tests, Agent 291 tests, contract 22 tests, and all portal tests;
4. Flutter dependency resolution, analysis, and all 32 tests;
5. EF pending-model check after the generated migration;
6. Release builds for API, portal, Agent, local host, and sync host with zero
   warnings/errors;
7. focused `dotnet format --verify-no-changes` for every new/edited .NET/Razor
   project/file, plus `dart format --output=none --set-exit-if-changed` if any
   Dart changes unexpectedly occur;
8. `git diff --check`;
9. residual direct-membership-query classification;
10. scans for secret/key/token/code patterns, personal emails/names, provider
    data, absolute paths, and generated build artifacts; and
11. exact base-to-head file/count/tree evidence.

The first local-engine test run can fail if its expected packaged Release hosts
have not been built; build them before claiming the test gate. Do not claim
Auth0 MFA, browser deployment, email, real-user, production-cookie, native, or
provider proof from synthetic tests.

## Implementation authorization boundary

This plan completes architecture/planning only. It authorizes no product source,
migration, dependency resolution/installation, Auth0 tenant/client/Action/MFA
mutation, real personal data, email/SMS, domain/DNS/TLS, deployment, Stripe or
other provider operation, staff Admin, Owner transfer, organization deletion,
production object storage, external Git action, native action, or destructive
cleanup.

The next bounded action is local milestone-7 implementation in a new scoped
`codex/` branch/worktree from the exact planning handoff commit, including the
three commits above, synthetic fixtures, generated migration, local dependency
restore, full validation, implementation evidence, and a new continuation
handoff. It requires fresh explicit Product Owner authorization.
