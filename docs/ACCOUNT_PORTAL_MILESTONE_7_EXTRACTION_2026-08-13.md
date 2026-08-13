# Account portal milestone 7 extraction — 2026-08-13

## Decision and exact base

- Exact base: `d468f38588d7ee760bbb2926b80d4e24268a7abd`
- Branch: `codex/account-portal-extraction`
- Worktree: `/private/tmp/showvault-account-portal-extraction`
- Roadmap source: item 2 in `docs/ROADMAP.md`

Stripe sandbox account proof remains unfinished because no browser session is
authenticated. Repeated Product Owner direction to start the next task is
applied here only to the next safe local roadmap step. It does not waive the
Stripe login requirement or authorize live Stripe, deployment, identity-
provider mutation, email delivery, personal data, or staff support access.

The roadmap's combined “customer account portal, membership/invitation
lifecycle, role administration, and internal support administration” item is
too broad for one safe milestone. The first bounded slice is:

**Sign in to a ShowVault-owned account website → select an organization → an
Owner lists active/suspended members and pending invitations → creates a
short-lived email-free invitation code for a non-Owner role → another signed-in
user accepts it once → the Owner changes that member's non-Owner role or
suspends/restores/revokes access → every existing tenant endpoint immediately
uses the new active-membership state.**

Internal ShowVault staff Admin is a later, separate trust domain.

## Current foundation

The repository already has:

- Auth0 JWT validation for normal users and a tightly guarded Development-only
  personal-beta scheme;
- organizations, venues, and a unique `(organization, identity subject)`
  membership relationship;
- five roles: Viewer, Technician, Manager, Administrator, and Owner;
- authenticated organization creation/listing and role-aware venue, Agent,
  recovery, synchronization, commercial, and billing endpoints;
- server-owned commercial and Stripe-provider projections; and
- a desktop Flutter application whose local scanning/recovery code imports
  `dart:io` and has no web target.

The current membership is an immutable record containing only role and creation
time. It has no lifecycle state, revision, update timestamps, invitation,
account audit, or centralized active-membership resolver. Authorization checks
are repeated across endpoints. Organization creation also needs an explicit
personal-beta rejection before account work can be considered commercial-safe.

There is no historical account-portal implementation to replay. Historical
commit `ce5be252fee9564b91872b9a6286d52f3f4d9e10` documented only the three-
surface intent: desktop, customer portal, and private staff Admin.

## Surface boundary

Milestone 7 should add a separate server-rendered .NET account website/BFF,
not compile the desktop Flutter application for web. The desktop contains
native filesystem and packaged-engine behavior that is intentionally absent
from an account website.

The proposed portal uses Auth0 Authorization Code + PKCE, a Secure/HttpOnly/
SameSite session cookie, antiforgery protection on mutations, no browser token
persistence, and a server-side typed client to the ShowVault API. Passwords,
MFA, passkeys, recovery, and password reset remain entirely in Auth0. No Auth0
Management API is required by this milestone.

The portal displays organization-owned account state only. Provider IDs,
webhook facts, payment data, secret material, raw identity-provider tokens,
filesystem data, backup contents, and internal support controls remain absent.

## Membership lifecycle

Extend membership with a closed state enum (`active`, `suspended`, `revoked`),
`UpdatedAt`, and an optimistic concurrency revision. Existing rows migrate to
`active`. Revoked rows remain for evidence and cannot authorize. A subject can
have only one row per organization. Invitation acceptance never creates a
duplicate or bypasses suspension/revocation; only the explicit Owner restore
action can reactivate a suspended member, while revoked-member re-entry needs a
separately reviewed policy.

Centralize membership lookup so every organization/venue/Agent/recovery/sync/
commercial/billing authorization path requires `active`. Do not rely on a
route ID, portal cookie role, JWT role claim, or stale client state.

Milestone 7 does not transfer, invite, demote, suspend, or revoke an Owner.
Only the current active Owner may administer non-Owner memberships. Owner
transfer and multi-Owner policy require a separate invariant and Product Owner
decision.

## Email-free invitations

An `OrganizationInvitation` stores:

- opaque ID and organization ID;
- target non-Owner role;
- an optional bounded organization-visible label, with its personal-data policy
  still requiring Product Owner approval before non-synthetic use;
- keyed HMAC-SHA256 token digest and rotation key ID, never the raw code;
- `pending|accepted|revoked|expired` state;
- creator subject, bounded timestamps, accepter subject when accepted; and
- a concurrency revision.

Creation returns a cryptographically random code exactly once. ShowVault sends
no email and stores no recipient email. The Owner shares the code out of band.
The invitee signs in, pastes it into a form, and submits it in a protected POST
body—never a URL, query, browser history, analytics field, or log. Codes expire
after a proposed seven days, are single-use, and are rate-limited by subject and
source address. Repeated acceptance by the winning subject is idempotent;
another subject, expired/revoked code, unsupported role, personal-beta identity,
or tenant conflict denies closed.

## Step-up and sensitive actions

Creating/revoking an invitation, changing a role, and suspending/restoring/
revoking a member require a fresh server-validated step-up claim. The local
contract should use a dedicated authorization policy with synthetic claim
fixtures and fail closed when the claim or freshness evidence is absent.
Exact Auth0 Action/MFA configuration and the claim namespace must be frozen in
a later operational plan before deployment; local implementation must not
pretend ordinary authentication is step-up proof.

Invitation acceptance requires normal authenticated identity but not an Owner
claim. Personal-beta identity is excluded from organization creation,
invitation creation/acceptance, membership administration, and all commercial
records.

## API contract proposed for implementation planning

- `GET /api/v1/organizations/{organizationId}/account/members`
- `GET /api/v1/organizations/{organizationId}/account/invitations`
- `POST /api/v1/organizations/{organizationId}/account/invitations`
- `POST /api/v1/organizations/{organizationId}/account/invitations/{id}/revoke`
- `POST /api/v1/account/invitations/accept`
- `PATCH /api/v1/organizations/{organizationId}/account/members/{membershipId}`

The PATCH accepts exactly one closed action (`change_role`, `suspend`,
`restore`, or `revoke`), an expected revision, and a role only for
`change_role`. It never accepts an identity subject, organization ID, Owner
role, provider ID, email, entitlement, or client-computed authority.

All list responses use membership/invitation IDs, organization-visible label,
closed state/role strings, timestamps, and revision only. They exclude identity
subjects and authentication claims.

## Durable audit and privacy

Add an append-only `AccountAuditEvent` with organization ID, server-known actor
subject, target membership/invitation ID, bounded action/outcome/reason,
correlation ID, policy version, and timestamp. It stores no raw invitation
code, email, name, access token, provider/payment data, IP address, user agent,
password data, or request body. Attempts that can be safely attributed to an
organization are recorded; invalid unknown codes return a uniform response and
do not create an oracle.

The first implementation must use synthetic identities and labels only. Real
people data, email delivery, exports, retention/deletion, legal holds, and
support impersonation require explicit policy and authorization.

## Required adversarial proof

Implementation is not complete without tests for:

- outsider, Viewer, Technician, Manager, Administrator, missing-subject, and
  personal-beta administration denial;
- missing/stale step-up denial;
- no Owner targeting or Owner-role invitation;
- raw code never stored and one-time readback only;
- invalid, expired, revoked, duplicated, raced, and cross-tenant acceptance;
- accepting subject already active/suspended/revoked;
- optimistic-concurrency conflicts and retry-safe idempotency;
- suspension/revocation immediately denying every existing tenant surface;
- restoration re-enabling only role-permitted actions;
- invariant-safe migration of existing memberships;
- uniform invalid-code responses and bounded rate limiting;
- append-only minimized audit evidence; and
- portal cookie, CSRF, redirect-origin, cache, and browser-token boundaries.

## Non-goals and remaining gates

This extraction authorizes no product-source implementation yet. It also does
not authorize Auth0 tenant/client/Action mutation, email/SMS, real personal
data, deployment/domain/DNS/TLS, staff Admin, support impersonation, Owner
transfer, organization deletion, provider financial actions, production object
storage, external Git actions, native packaging, or destructive cleanup.

The next bounded step is reconstruction/architecture review of this proposed
milestone, including the portal project boundary, centralized membership
authorization migration, exact state machine, migration plan, step-up claim
contract, API shapes, and test matrix. Product-source implementation requires a
fresh explicit authorization after that plan is reviewed.
