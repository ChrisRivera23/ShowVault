# Local-first milestone 6 planning handoff — 2026-08-13

## Checkpoint

- Planning branch: `codex/local-first-milestone-6-plan`
- Planning worktree: `/private/tmp/showvault-local-first-m6-plan`
- Exact milestone-5 base:
  `92a367fcefc1aa91522c7c1f648c1cebeed4f21f`
- Extraction/architecture commit:
  `1d32d96f956b09eaee4a8db8e3e4bc1124c6a795`
- Exact outcome: **Sign in as an organization Owner → choose one
  server-approved offering in Plan and storage → continue to Stripe-hosted
  Checkout → return with access still pending → accept and reconcile signed
  provider events into ShowVault's license/subscription projection → refresh
  the normalized plan → open a short-lived Stripe Billing Portal session**

Read `docs/LOCAL_FIRST_MILESTONE_6_EXTRACTION.md`,
`docs/LOCAL_FIRST_MILESTONE_6_RECONSTRUCTION_REVIEW_2026-08-13.md`, and
`docs/ACCOUNT_BILLING_ADMIN_ARCHITECTURE.md` completely before acting from this
checkpoint.

## Completed planning

Exactly two historical commits were selected and accounted for: `eea1d45` and
`ce5be25`. Their 13 unique paths all overlap the current tree and were treated
as provenance/design evidence rather than replayable code.

The reconstruction contract now fixes:

- the existing milestone-5 projections and evaluator as entitlement authority;
- Owner-only exact-tenant Checkout and Billing Portal session creation from a
  closed internal offering catalog and fixed return origins;
- subscription-mode hosted Checkout with one recurring and one one-time Price,
  while exact products/prices/tax/payment policy remain unselected;
- durable server-generated idempotency and provider/environment-unique
  organization bindings;
- redirect completion as non-authoritative pending presentation;
- exact raw-body signature verification before parsing, a minimal no-payload
  durable inbox, concurrent duplicate handling, and bounded event families;
- current-provider-state reconciliation for out-of-order events before
  transactionally changing normalized license/subscription state;
- no implicit past-due grace, fail-closed refund/dispute/unsupported-state
  attention, and no deletion or stranding of recovery data;
- ephemeral Checkout/Portal URLs and exclusion of payment/provider details
  from public plan, local SQLite, logs, and general audit; and
- disabled-by-default provider configuration with deterministic synthetic
  adapters/signed fixtures for implementation tests.

Planning reviewed current official Stripe Checkout, fulfillment, webhook,
subscription, portal, and key-management documentation. It did not access a
Stripe account or invoke a provider API.

## Authorization boundary

This checkpoint completes extraction and architecture planning only. It does
not authorize implementation.

Do not modify application/API/engine source or migrations; install the Stripe
plugin, CLI, SDK, or another dependency; access or create a Stripe account,
product, Price, Customer, Checkout/Portal session, endpoint, key, or secret;
use payment/customer data or credentials; register a webhook; mutate cloud
resources; build/install meaningful native packages; fetch/push Git state;
create/mutate a PR; dispatch workflows; deploy; charge/refund; or clean up
destructively without new explicit authorization.

## Next gated action

Stop for Product Owner direction. The next bounded action is implementation of
the exact milestone-6 Stripe-hosted funnel using disabled-by-default provider
configuration, deterministic synthetic adapters, and locally generated signed
fixtures only. It requires separate explicit implementation authorization.

Stripe sandbox/account provisioning and operational proof require another
explicit authorization after local implementation. A ShowVault-owned account
website, membership/role administration, staff Admin, production hosted-object
storage, and native proof remain separate future slices.
