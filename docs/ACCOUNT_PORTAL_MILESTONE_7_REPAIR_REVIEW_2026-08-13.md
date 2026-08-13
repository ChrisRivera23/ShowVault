# Account portal milestone 7 repair review — 2026-08-13

## Review checkpoint and result

- Reviewed head: `a874a49c2b9c68fb9fa7d4cef7fdaab988ac56f1`
- Adversarial-review base: `549a91f01ca678ac8a7535ae1f95445009a2be05`
- Repair commits: `22212c8`, `5763fe3`, `ffa996e`, `a874a49`
- Branch: `codex/account-portal-m7-repair`
- Worktree: `/private/tmp/showvault-account-portal-m7-repair`

Result: **changes required before integration; do not operationalize or deploy
milestone 7.**

The product repairs for personal-beta hosted-sync denial, invitation
expiry/race recovery, database-aware bounded key rotation, chunked-body limits,
accepted-membership linkage, Auth0 `audience`, exact portal origin, generic
errors, missing rendering, and bounded Development stores are directionally
sound. API 105 and portal 15 tests pass at the reviewed head. The remaining
blocker is that the frozen proof gate is still not complete, plus two bounded
invitation-token hygiene/order defects.

This review changes documentation only. It does not change product source,
migrations, dependencies, Auth0, Stripe, deployment, production enablement,
personal data, or external state.

## Release blocker

### [P1] The repaired evidence still overclaims the frozen proof matrix

The evidence says the locally testable repair gate is satisfied, and repair
commit `ffa996e` is named “Complete account portal adversarial proof matrix.”
The passing counts are accurate, but several scenarios explicitly required by
the reconstruction review and first adversarial review remain unexecuted:

- `HostedSyncTests.Personal_beta_identity_cannot_use_hosted_sync` proves direct
  scan/list remains available and denies hosted-sync `begin` and `receipt`.
  It does not create a session under an ordinary identity and then prove that
  the same personal-beta subject cannot retrieve file state, append a chunk, or
  commit that existing session. Those paths share a source guard, but the frozen
  gate required endpoint proof for begin, append, commit, and state retrieval.
- The ordinary OIDC challenge test parses `audience`, response type, PKCE,
  state, and nonce, but does not assert the exact `redirect_uri`, exact ordinary
  scope, or absence of `offline_access`. The step-up test invokes the event
  delegate directly rather than parsing a real step-up challenge, so it does
  not prove that PKCE/state/nonce/callback and the MFA parameters coexist in the
  emitted authorization URL. Hostile `X-Forwarded-Host` behavior is also not
  exercised.
- `MembershipEndpointStateTests` proves suspended/revoked denial through all
  eight endpoint modules, but its active fixtures use only Manager for five
  modules and Owner for three. It does not implement the frozen per-consumer
  active Viewer/Technician/Manager/Administrator/Owner role matrix or
  wrong-tenant/wrong-venue matrix. Other older tests cover portions of that
  space, not the complete eight-consumer cross-product.

Add these missing cases and change the evidence only after their exact scenario
counts pass. Until then, the source guards can be reviewed as plausible but the
required regression contract is not frozen.

## Bounded findings

### [P2] Malformed invitation codes perform database work before rejection

`AccountAdministrationService.AcceptInvitationAsync` calls
`HasCompleteKeyRingAsync` before `CandidateDigests`. The preflight queries all
pending invitation key references, so a blank, oversized, or malformed code
reaches the database before the pure bounded parser rejects it. This reverses
the frozen acceptance sequence (“reject personal beta and malformed code before
database work”) and returns feature-unavailable rather than the uniform invalid
invitation result when configuration is incomplete.

Compute and reject malformed candidate input first, then run the database-aware
key-ring preflight only for a structurally valid code. Add a test using an EF
command interceptor or equivalent query counter to prove malformed input causes
zero database commands.

### [P2] Temporary invitation secret buffers are not cleared

`InvitationTokenService.Issue` leaves the 32 random code bytes in a managed
buffer after encoding/digesting, and `CandidateDigests` leaves decoded raw-code
bytes in memory after computing candidates. Repeated `TryKeys` calls also decode
fresh key-secret arrays even for availability checks that immediately discard
them. The original key-ring repair explicitly required zeroing temporary secret
buffers where practical.

Use `CryptographicOperations.ZeroMemory` in `finally` blocks for generated and
decoded raw-code buffers. Either validate/cache key material once for the
singleton lifetime or explicitly clear discarded decoded key arrays on every
failure/read-only path without clearing arrays still in use.

## Verified repairs

- Every hosted-sync entry point now denies personal beta before its relevant
  object-store, reservation, session, or receipt operation.
- Same-subject invitation acceptance reloads the exact accepted invitation and
  membership after a database race; other subjects retain the uniform denial.
- Acceptance, revoke-at-expiry, and invitation listing persist observed expiry
  with concurrency handling.
- Key IDs are normalized and bounded, null/duplicate/oversized rings deny
  closed, and pending unexpired retiring-key references block premature removal.
- Account request parsing independently enforces 4096 bytes for chunked bodies.
- The optional accepted-membership relationship has an indexed restrictive
  foreign key and EF reports no pending model change in the repair evidence.
- Portal authorization redirects set Auth0 `audience`; configured origin checks
  scheme/host/port before authentication; enabled host filtering is explicit;
  generic failures retain security headers; and ephemeral stores evict at fixed
  capacities.
- Organization/error rendering, server-side bearer-token use, subject
  exclusion, successful antiforgery mutation, and one-time invitation reveal
  are exercised with synthetic authenticated navigation.

## Verification performed for this review

- API: 105 passed, 0 failed, 0 skipped.
- Account portal: 15 passed, 0 failed, 0 skipped.
- Worktree was clean before this documentation-only review.
- The reviewed base-to-head diff contains 25 files and no generated build
  artifacts in Git status.

The earlier full-suite, Release-build, EF, formatting, Flutter-analysis, and
credential-scan results remain valid evidence for `a874a49`; this review does
not reinterpret synthetic tests as real Auth0, production browser, provider,
deployment, native, or real-person proof.

## Next gate

The next bounded local task is a proof-completion patch plus the two invitation
token fixes above, followed by another fresh review. Integration, deployment,
Auth0 operational configuration, durable production portal sessions, Stripe
sandbox operations, real-person onboarding, and production enablement remain
separately gated.

## Subsequent remediation checkpoint

After this review, the Product Owner separately authorized its bounded next
task. Commit `1541d5d` addresses the findings without changing the historical
verdict for reviewed head `a874a49`:

- malformed invitation codes now complete bounded parsing before the database
  key-ring preflight, with an EF command-interceptor assertion proving zero
  reader commands;
- generated and decoded raw invitation-code buffers and transient decoded key
  arrays are cleared with `CryptographicOperations.ZeroMemory`;
- the personal-beta fixture now starts an ordinary hosted-sync session and
  proves file-state, append, commit, and receipt denial while direct scan/list
  remains available;
- ordinary and step-up tests parse real challenge URLs and assert audience,
  exact callback/scopes, no `offline_access`, PKCE, state, nonce, plus MFA
  `acr_values` and `max_age`; a hostile forwarded host is ignored; and
- the eight endpoint modules now execute all 40 active role combinations, 16
  suspended/revoked cases, eight wrong-tenant/wrong-venue cases, and restoration.

Post-remediation validation passes API 154, portal 15, Platform 30,
local-engine 67, contracts 22, Agent 291, and Flutter 32 tests. Flutter analysis,
EF pending-model check, five Release builds, focused formatting, and diff checks
are clean. A fresh review of `1541d5d` remains the next gate; this appendix does
not self-approve integration or production operation.
