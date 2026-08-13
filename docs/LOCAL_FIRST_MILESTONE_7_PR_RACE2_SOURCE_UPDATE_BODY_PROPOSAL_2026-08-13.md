## Summary

Advance ShowVault from the PR #24 foundation through the reviewed local-first
milestone 7 candidate and its two bounded PR-CI concurrency repairs. This
introduces a complete local recovery path, guarded hosted synchronization and
commercial state, a disabled-by-default Stripe sandbox seam, and a secure
organization account portal.

The branch is a linear successor to exact `main`
`32c21cfbd51ea5f16bb5fe84c56f4efb125b1df4`. It is 57 commits ahead and zero
behind, with 194 changed files, 29,288 insertions, and 296 deletions.

## What changed

### Local-first desktop recovery

- add exact, consented desktop catalog scanning with tenant-scoped persistence;
- create bounded local recovery packages and a durable local vault/queue;
- verify saved evidence before reporting success and repair interrupted state;
- add attended, controlled local restore with ambiguity-preserving behavior;
- synchronize only verified local recovery points after explicit signed-in
  consent; and
- package the local/sync engine hosts for the Flutter desktop application.

### Hosted and commercial boundaries

- add hosted-sync sessions, bounded object handling, recovery-candidate state,
  and fail-closed authorization;
- enforce normalized commercial entitlement before hosted mutations;
- model license, subscription, billing attempt, receipt, reconciliation, and
  attention state without using redirect completion or client claims as access
  authority;
- add a direct HTTP Stripe sandbox adapter with signed webhook verification and
  exact configured offerings; and
- keep all checked-in billing/provider configuration disabled and secret-free.

### Organization account portal

- centralize active organization-membership authorization across all protected
  API surfaces;
- add single-use invitation codes, membership lifecycle and role controls,
  minimized append-only audit, and MFA-backed step-up enforcement;
- add a server-rendered Razor Pages BFF with server-side authentication tickets,
  antiforgery, configured-origin enforcement, bounded ephemeral stores, and
  generic error handling; and
- keep checked-in invitation and portal configuration disabled and fail closed
  without production-grade distributed session/Data Protection storage.

### Review, hardening, and PR-CI repairs

- deny personal-beta identities across hosted-sync mutation paths while
  retaining the bounded direct-scan path;
- prove invitation race, expiry, key-rotation, malformed-input, and secret-buffer
  handling;
- exercise the complete role/surface, suspended/revoked, and tenant/venue
  authorization matrix;
- verify ordinary and MFA step-up authorization-code redirects with PKCE, state,
  nonce, exact audience, and no `offline_access`;
- apply the formatter-only repair approved by the final local integration gate;
- retry only incomplete post-conflict invitation-winner observations within a
  cancellation-aware 310-millisecond bound; and
- re-observe an accepted invitation or membership that appears after the
  invitation read, preventing a stale pending object from denying the matching
  winner while preserving different-subject and unrelated-member denial.

## Validation

Complete local validation passed on exact second repaired product head
`0e00171`:

- 583 .NET tests: Platform 30, local engine 67, contracts 22, Agent 291, API
  158, and account portal 15;
- 32 Flutter tests and clean Flutter analysis;
- the original same-subject/different-subject concurrency scenario passed 80/80
  repetitions across four concurrent test processes;
- deterministic tests cover delayed post-conflict visibility, a winner committed
  between the invitation and membership queries, immediate different-subject
  denial, and bounded retry exhaustion;
- no pending EF Core model changes;
- zero-warning Release builds for API, account portal, Agent, local-engine host,
  and sync-engine host;
- formatting verification for every changed C# file; and
- clean diff and focused credential/private-key checks.

At prior exact source `3f2496a`, pull-request run `31707204442` passed API and
Flutter. Simultaneous push run `31707198609` passed Flutter but failed the API
concurrency test, exposing the stale pending-invitation/newly-visible-membership
interleaving addressed by the second repair. Fresh push and pull-request CI must
both run on updated source `0e00171` and pass before any later readiness
decision.

The only existing CI annotations are inherited, non-failing Node.js 20
deprecation notices for existing GitHub Action versions.

## Review checkpoints

- exact second repaired product and proposed PR source:
  `0e00171f16ae4feca682de916cb29c862fe840ec`;
- source parent and prior published PR source:
  `3f2496a41c7f5ec359971b5dc206e6a42159e798`;
- source tree: `8e68dc94b9793fdf494fa14f6950fc1f6370956f`;
- changed-path SHA-256:
  `8cffcf6cc7a96a7574661306c8ad1c88448b8254bd9adde1ce73b2ea7c0d9a09`;
- binary-diff SHA-256:
  `2c8e6df18e616c880c6e505bb2ba877fbc17f8e38fe052e130a0ce0734999e5f`;
- focused second-repair changed-path SHA-256:
  `c2d17850eb97c05592958a636ca4b1006e8592b48e0c5299bac3a320bd018260`;
  and
- focused second-repair binary-diff SHA-256:
  `c6eed69fd1614e17c58423e159c1e466ca1b57afb74ea3206e7f59911575ca79`.

## Operational boundaries

This PR contains local synthetic implementation and proof. It does not claim or
perform production deployment or enablement, database migration application,
Auth0 tenant/client/Action configuration, real MFA/browser proof, Stripe object
or account mutation, real customer/payment/personal data, real charges or
refunds, durable production portal sessions/Data Protection keys, production
hosted storage, native installation/signing/protocol proof, or release activity.

All such operations remain separate, explicitly authorized gates.
