# Local-first milestone 5 planning handoff — 2026-08-13

## Checkpoint

- Planning branch: `codex/local-first-milestone-5-plan`
- Planning worktree: `/private/tmp/showvault-local-first-m5-plan/worktree`
- Exact milestone-4 base:
  `80fb1092df254f2f6bd3d11b209634b52beb7a15`
- Extraction/architecture commit:
  `a541c88b815e99ffcb64d00f0c9b15ffe11499b6`
- Exact outcome: **Sign in as an organization Owner → open Plan and storage
  → review server-derived license/subscription eligibility and logical hosted
  usage → allow or deny each new hosted-sync reservation from the same
  projection → retain path-free audited evidence**

Read `docs/LOCAL_FIRST_MILESTONE_5_EXTRACTION.md`,
`docs/LOCAL_FIRST_MILESTONE_5_RECONSTRUCTION_REVIEW_2026-08-13.md`, and
`docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md` completely before acting from this
checkpoint.

## Completed planning

Exactly four historical commits were selected and accounted for: `c683b8f`,
`e017769`, `eea1d45`, and `805a96c`. The final two are sibling/source variants,
not a replayable linear series. All 40 unique selected paths overlap the
current tree and were reconciled as design evidence.

The reconstruction contract now fixes:

- independent organization-scoped license and subscription projections plus a
  deterministic server-side entitlement evaluator;
- an Owner-only, read-only plan/storage response with no provider or payment
  details;
- existing Manager/Administrator/Owner hosted-sync authorization followed by
  a bounded commercial decision for a new session;
- organization-wide logical-byte quota, atomic full-manifest reservation,
  idempotent begin, and exactly-once reservation-to-commit accounting;
- continued append/commit/receipt recovery for an already reserved session
  after a later commercial state change;
- append-only minimized decision evidence and path-free local/UI denials;
- explicit synthetic Development/test records only, with absent or unsupported
  Non-Development state denying new sessions; and
- no payment provider, checkout, webhooks, prices, account/role mutation,
  portal, internal Admin, deletion/reclamation, production storage, cloud
  operation, or native proof.

The roadmap now separates this provider-independent boundary from later
provider-backed billing and later membership/staff administration.

## Authorization boundary

This checkpoint completes extraction and architecture planning only. It does
not authorize implementation.

Do not modify application/API/engine source or migrations; install or contact a
billing provider; use credentials, payment/customer/venue data, or cloud
resources; build/install meaningful native packages; fetch/push Git state;
create/mutate a PR; dispatch workflows; deploy; or perform destructive cleanup
without new explicit authorization.

## Next gated action

Stop for Product Owner direction. The next bounded action is implementation of
the exact milestone-5 provider-independent commercial projection, Owner plan
view, and hosted-sync reservation contract using synthetic local fixtures only.
It requires separate explicit implementation authorization.

Provider-backed billing/webhooks, membership administration, staff Admin,
production object storage, and native proof remain independent future slices.
