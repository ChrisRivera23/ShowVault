# Account portal milestone 7 implementation evidence — 2026-08-13

## Exact checkpoint

- Planning base: `6985767a53e244145c3a124a0e446d81404d228e`
- Branch: `codex/account-portal-m7-implementation`
- Worktree: `/private/tmp/showvault-account-portal-m7-implementation`
- Commit 1: `bb31c64f2dc12bce8f7f6da6018f9eb00ee6d019`
  (`Centralize active membership authority`)
- Commit 2: `f25077800690fe97017d9860c78474d7c2705f32`
  (`Add invitation and account administration API`)
- Commit 3: `74db2fb744ff445697555900eaaf419823350e64`
  (`Add secure account portal BFF`)
- Adversarial-review base: `549a91f01ca678ac8a7535ae1f95445009a2be05`
- Repair branch: `codex/account-portal-m7-repair`
- Repair commit 1: `22212c8` (`Repair account API safety invariants`)
- Repair commit 2: `5763fe3` (`Repair account portal security contract`)
- Repair commit 3: `ffa996e` (`Complete account portal adversarial proof matrix`)

The repaired implementation spans 71 files from the planning base. The repair
changes 25 files from the adversarial-review base, including this evidence and
the continuation handoff. Generated `bin` and `obj` outputs remain ignored and
uncommitted.

## Implemented boundary

Commit 1 replaces positional memberships with revisioned
`Active|Suspended|Revoked` domain state, adds invitation and append-only account
audit entities, and includes generated migration
`20260813075629_AddAccountMembershipLifecycle`. The migration explicitly
backfills existing membership state, update time, and revision before applying
required constraints. A single scoped membership authority now owns every
human tenant authorization query; all eight former endpoint consumers require
active membership. Personal-beta authentication cannot create organizations.

Commit 2 adds a disabled-by-default invitation key ring, 32-byte CSPRNG codes
encoded as exactly 43 base64url characters, HMAC-SHA256 storage, active plus
retiring key verification, strict step-up scope/MFA/`iat` validation, minimized
transactional account audit, rate limits, strict request parsing, and all six
reviewed account routes. Responses never contain identity subjects, digests,
key IDs, claims, or provider data. The raw code is returned only by the create
response and is never logged or stored.

Commit 3 adds a separate net10 Razor Pages BFF. It uses confidential OIDC
Authorization Code with PKCE, an opaque cookie backed by a server-side ticket
store, Secure/HttpOnly cookie rules, strict antiforgery, no-store/security
headers, closed API DTOs, bounded response buffering, server-side bearer-token
use, local return paths, and one-time server-side invitation-code display. It
contains no Flutter/local-engine dependency, browser token storage, analytics,
or `offline_access`. Checked-in configuration is disabled and secret-free.

The repair closes every finding in the adversarial review. Personal-beta
identities retain direct recovery-candidate scan/list access but are rejected
before all hosted-sync state or object-store access. Invitation acceptance now
persists observed expiry, recovers an exact same-subject concurrency winner,
and binds accepted invitations to memberships through a restrictive foreign
key. Key configuration is normalized and bounded to active plus retiring keys,
and refuses premature removal while an unexpired pending invitation references
the retiring key. Account JSON is bounded independently of `Content-Length`.

The portal now sends Auth0's exact `audience` authorization parameter on
ordinary and step-up redirects, enforces the configured HTTPS scheme/host/port,
sets the enabled host allowlist, returns generic correlation-only failures,
bounds both Development-only ephemeral stores, renders organization/error
context, and retains the production startup denial.

Only an in-memory ticket store exists in this milestone, so enabled production
startup deliberately fails closed. A real durable encrypted/distributed ticket
store and persistent Data Protection key ring remain a separate operational
implementation and proof gate.

## Verification evidence

Passing suites:

- local engine: 67;
- platform: 30;
- API: 105;
- agent contracts: 22;
- agent: 291;
- account portal: 15; and
- Flutter: 32, with `flutter analyze` reporting no issues.

The 105 API cases include these exact repair additions over the original 68:

- four body/key/personal-beta safety cases;
- fifteen account-administration cases covering every non-Owner role,
  outsider/missing-subject/personal-beta, revocation, cross-tenant IDs,
  same-subject and other-subject races, existing active/suspended/revoked
  subjects, persisted expiry, role/state/revision rules, concurrent mutation,
  rate limiting, retiring-key acceptance, and missing-pending-key denial;
- seventeen active-versus-suspended/revoked endpoint cases across Tenant,
  Recovery Candidates, Recovery History, Agent Enrollment, Commercial Plan,
  Billing, Hosted Sync, and Account Administration, including role-preserving
  restoration; and
- one additional closed step-up-claim case. The hosted-sync personal-beta case
  also proves its guarded direct recovery scan/list path remains available.

The 15 portal cases include the original eight plus bounded-store eviction,
exact-origin and actual OIDC redirect parsing (`audience`, code response, PKCE,
state, and nonce), step-up audience/scope/`acr_values`/`max_age`, generic error
redaction, authenticated organization/member rendering, successful antiforgery
mutation, bearer-token/subject exclusion, one-time invitation-code refresh,
typed forbidden-to-step-up mapping, and rendered uniform invitation failure.

Release builds completed with zero warnings/errors for API, account portal,
Agent, local-engine host, and sync-engine host. EF reports no pending model
changes. Focused format verification for every milestone file, Dart formatting,
and `git diff --check` pass.

The residual non-migration `Memberships` references are classified as:

- DbSet/configuration in `PlatformDbContext`;
- centralized reads in `MembershipAuthorizationService`;
- account administration persistence in `AccountAdministrationService`; and
- new-owner creation in `TenantEndpoints`.

There is no direct endpoint authorization query outside the central service.
Secret/live-key/private-key scans found no production credential. Absolute-path
matches are confined to historical documentation checkpoint metadata.

The repository-wide platform/API formatting commands also report baseline
whitespace findings in untouched `CommercialEntitlementTests.cs` and
`BillingService.cs`. Focused verification of all files changed by this milestone
is clean; unrelated baseline files were preserved.

## Authorization and proof limits

This was local implementation and synthetic validation only. It did not mutate
the Auth0 tenant/client/Actions/MFA, Stripe resources, real people, email/SMS,
DNS/TLS, deployment, production secrets, external Git state, native packages,
or customer/payment data. It does not prove a real Auth0 login, real MFA
step-up, production cookies, a deployed browser flow, or provider operation.

During the authorization turn, the open Chrome Stripe tab was confirmed already
authenticated at the **ShowVault Pro sandbox** test dashboard. No Stripe object
or API mutation was performed; the authorization was applied to this planned
milestone-7 implementation.

## Next gated action

The locally testable repair gate is satisfied. The next safe action is a fresh
review of the repair commits and this exact evidence before integration.
Auth0 operational configuration/deployed proof, durable production portal
sessions, real-person onboarding/privacy policy, production enablement, and
Stripe sandbox object proof remain independent, explicitly authorized gates.
