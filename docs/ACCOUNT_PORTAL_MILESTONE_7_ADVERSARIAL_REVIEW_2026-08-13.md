# Account portal milestone 7 adversarial review — 2026-08-13

## Review checkpoint and result

- Reviewed head: `e0b8863ad84a3d85d31c1ed7c9ec6e6a1ea365f2`
- Planning base: `6985767a53e244145c3a124a0e446d81404d228e`
- Implementation commits: `bb31c64`, `f250778`, `74db2fb`
- Branch: `codex/account-portal-m7-implementation`
- Worktree: `/private/tmp/showvault-account-portal-m7-implementation`

Result: **changes required; do not operationalize or deploy milestone 7.**

The central active-membership design, domain transition restrictions, migration
backfill, disabled checked-in configuration, raw-code hashing, step-up predicate,
opaque ticket handle, antiforgery cookie, security headers, and production
in-memory-store denial are sound foundations. The implementation is not yet the
frozen outcome because three release blockers and five bounded findings remain.

This review changed documentation only. It did not change product source,
migrations, dependencies, Auth0, Stripe, deployment, personal data, or external
state.

## Release blockers

### [P1] Personal beta can mutate hosted-sync commercial state

`HostedSyncEndpoints` authorizes every begin/read/append/commit/receipt path with
active role/venue checks only. Unlike organization, account, and billing
mutations, it never rejects `HumanIdentity.IsPersonalBeta(user)`. A pre-seeded
personal-beta manager can therefore create hosted-sync sessions, reserve or
commit commercial storage, and write synthetic hosted objects. This violates the
frozen rule that personal beta retains only its guarded local Scan use and is
denied commercial/provider mutation.

Repair all hosted-sync entry points through one shared denial before any object
store, session, reservation, commercial audit, or database mutation. Add a
regression proving personal beta can still submit/list direct recovery scans but
cannot begin, append, commit, or retrieve hosted-sync state.

### [P1] The portal does not reliably request an Auth0 API audience

`Program.cs` assigns `OpenIdConnectOptions.Resource` from `Auth0Audience`. Auth0
documents `audience` as the normal required parameter for a custom API access
token; `resource` is an alternative only under the tenant's Resource Parameter
Compatibility Profile. This implementation neither sends `audience` nor freezes
or proves that compatibility profile. A normal Auth0 tenant can therefore return
an opaque `/userinfo` token or a token unusable by ShowVault API, making both
ordinary portal reads and MFA-step-up mutations fail.

Set the exact `audience` authorization parameter on ordinary and step-up
requests, retain exact scopes, and add a redirect-level test that parses the
actual OIDC challenge URL and asserts `audience`, PKCE, state, nonce, callback,
scope, MFA `acr_values`, `max_age`, and absence of `offline_access`.

### [P1] Required adversarial coverage is missing and prior evidence overclaims it

`MembershipAuthorizationTests` calls the central service but does not exercise
the eight endpoint consumers with active/suspended/revoked fixtures.
`AccountAdministrationTests` has three scenarios and omits concurrent
acceptance/mutation, expiry persistence, invitation revocation, role change,
restore/revoke, cross-tenant IDs, existing active/suspended/revoked subjects,
other-winner replay, audit rollback, rate-limit rejection, and rotation through
the API. `PortalSecurityTests` has five test methods (eight xUnit cases after one
theory) but no synthetic authenticated navigation, successful antiforgery POST,
OIDC parameter inspection, typed-client error mapping, member subject-exclusion
render, one-time raw-code render/refresh, or mutation-page proof.

The implementation evidence's “full validation” description is therefore not
supported by the passing test counts. Build/test success remains real, but it
does not prove the frozen security matrix. Add the missing matrix and amend the
evidence with exact scenario counts before approval.

## Bounded findings

### [P2] Concurrent acceptance is not idempotent for the winning subject

On any `DbUpdateException`, `AcceptInvitationAsync` rolls back, clears tracking,
and returns `invitation_unavailable`. If two requests from the same subject race,
one can commit and the other returns 400 even though it is the exact winner.
After the conflict, reload the invitation and membership without tracking and
return the existing membership only when accepted subject and membership ID
match exactly. Other subjects must keep receiving the uniform unavailable error.

### [P2] Acceptance observes expiry without persisting it

`AcceptInvitationAsync` calls `ObserveExpiry`, then immediately returns when the
state becomes `Expired`; no transaction or save occurs. The scoped context is
disposed, leaving the database row pending. Persist the expiry under revision
concurrency before returning the uniform unavailable response. Apply the same
rule to revoke-at-expiry and make list sweeps concurrency-safe.

### [P2] Invitation key-ring validation is not closed or bounded

Key IDs are not length-bounded, uniqueness is checked before trimming, and a
pair such as `"active"`/`" active "` can reach `SingleOrDefault` as duplicate
normalized IDs and throw rather than disabling invitation create/accept. Null
configured collections can also fault. Pending invitations do not protect a
retiring key from premature removal.

Normalize first, require 1–80 characters, reject null/duplicates without
throwing, zero temporary secret buffers where practical, and add a database-aware
preflight that refuses configuration which omits a key still referenced by a
pending unexpired invitation.

### [P2] Portal origin and generic-error contracts are not implemented

`AccountPortalOptions.Origin` is validated but never used. `AllowedHosts` remains
`*`, and OIDC callback/logout URLs are derived from the incoming request host
rather than checked against the configured origin. The app also has no exception
handler or generic bounded error endpoint; unexpected API/configuration failures
can bypass the reviewed correlation-only error contract.

Reject non-matching scheme/host/port before authentication, set explicit allowed
hosts, derive external callback/logout URLs from the frozen origin or trusted
forwarded-header configuration, and add a generic no-store error endpoint with a
bounded correlation ID. Test hostile Host/forwarded-host values and exception
responses. Production must remain disabled until durable ticket storage exists.

### [P2] Account request bodies are not actually bounded to 4096 bytes

`AccountEndpoints.ParseAsync` rejects a declared `Content-Length` above 4096,
but a chunked request has no length and is passed to `ReadFromJsonAsync`, leaving
the server-wide request limit as the effective bound. Enforce a stream/body
limit independent of `Content-Length`, reject surplus bytes, and test chunked
oversize JSON. `MaximumCodeBytes` should either enforce a real input bound or be
removed rather than acting only as key-ring configuration validation.

## Lower-severity completeness notes

- The portal member page does not display the selected organization name.
- Failed invitation acceptance sets a model error, but the Razor page has no
  validation summary, so the generic message is not rendered.
- Invitation `AcceptedMembershipId` is not protected by a database foreign key;
  adding a restrictive optional relationship would strengthen evidence integrity.
- Expired server ticket and one-time-secret entries are removed only on lookup;
  this is acceptable for the Development-only implementation but should use
  bounded eviction even before production storage is added.

## Repair sequence and acceptance gate

Use a new local `codex/` repair branch/worktree from this review commit:

1. API safety repair: personal-beta hosted-sync denial, acceptance expiry/race
   recovery, bounded key-ring validation, request-body bounding, and restrictive
   accepted-membership linkage if migration policy approves it.
2. Portal contract repair: exact Auth0 `audience`, configured-origin enforcement,
   generic errors, organization/error rendering, and bounded ephemeral stores.
3. Proof repair: implement the complete required API/portal matrix, rerun all
   repository suites/builds/scans, and replace overbroad evidence claims with
   exact scenario evidence.

Do not configure Auth0, create Stripe objects, deploy, use real people, or enable
production as part of this repair. Product-source repair requires fresh explicit
Product Owner authorization.

## Source verification

Auth0's current authorize documentation defines `audience` as the unique target
API identifier and describes `resource` as an alternative only when the Resource
Parameter Compatibility Profile is set to compatibility:

- https://auth0.com/docs/api/authentication/authorization-code-flow/authorize-application
- https://auth0.com/docs/get-started/authentication-and-authorization-flow/authorization-code-flow/call-your-api-using-the-authorization-code-flow
