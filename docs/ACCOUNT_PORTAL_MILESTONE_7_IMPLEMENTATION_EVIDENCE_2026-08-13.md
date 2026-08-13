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

The implementation spans 63 files from the planning base. Generated `bin` and
`obj` outputs remain ignored and uncommitted.

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

Only an in-memory ticket store exists in this milestone, so enabled production
startup deliberately fails closed. A real durable encrypted/distributed ticket
store and persistent Data Protection key ring remain a separate operational
implementation and proof gate.

## Verification evidence

Passing suites:

- local engine: 67;
- platform: 30;
- API: 68;
- agent contracts: 22;
- agent: 291;
- account portal: 8; and
- Flutter: 32, with `flutter analyze` reporting no issues.

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

The next safe action is a fresh adversarial code review of these three commits
against the frozen architecture and evidence, followed by repairs if findings
exist. Auth0 operational configuration/deployed proof, durable production portal
sessions, real-person onboarding/privacy policy, and Stripe sandbox object proof
remain independent, explicitly authorized gates.
