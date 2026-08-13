# Stripe sandbox local checkpoint — 2026-08-13

## Exact boundary

- Base: `bb39cf630a67bedc9fa431cd5efbbbd280676247`
- Branch: `codex/stripe-sandbox-proof`
- Worktree: `/private/tmp/showvault-stripe-sandbox-proof`
- Implementation/preflight commit:
  `fd9a7d299034275446c441375a485ae88aef0ede`
- Scope: 9 files, `+937/-6`

This checkpoint completes the local portion of the authorized Stripe sandbox
operational slice. It is not Stripe-account-backed proof.

## Implemented

- disabled-unless-complete sandbox configuration;
- restricted sandbox key (`rk_test_`) enforcement;
- exact `2026-07-29.dahlia` request pinning;
- direct bounded HTTP calls with request timeout and 2 MiB response limit;
- no HTTP client request/response logging;
- fixed two-item subscription-mode Checkout with idempotency, expiry, fixed
  return routes, and bounded opaque correlation metadata;
- fixed Billing Portal creation for an already-bound Customer;
- exact Stripe hosted-domain validation;
- current-state retrieval for Checkout, Subscription, Invoice, Charge, and
  Dispute events;
- Dahlia subscription-item periods, invoice parent links, Invoice Payments,
  and invoice-line pricing; and
- state-digest reconciliation cursors that process changed current state at an
  equal provider timestamp while deduplicating an identical revision.

The checked-in configuration remains `Enabled: false` and contains no key,
secret, Price ID, account data, or customer/payment data.

## Validation

- API: 50 passed.
- Platform: 23 passed.
- Local engine: 67 passed after building both packaged Release hosts; the first
  fresh-worktree attempt truthfully failed only because those expected Release
  host binaries had not yet been built.
- Agent: 291 passed.
- Agent contracts: 22 passed.
- Flutter: 32 passed; analysis reported no issues.
- API, local host, and sync host Release builds: zero warnings/errors.
- EF: no pending model changes.
- Focused .NET whitespace verification for every new/edited adapter/config/test
  file, `git diff --check`, and a repository secret-pattern scan passed.

## External-state proof

No Stripe CLI, SDK, plugin, or other provider dependency was installed. No
Stripe API request, Product, Price, Customer, Checkout Session, Portal Session,
event destination, key, endpoint secret, charge, refund, dispute, or account
mutation occurred. Neither available browser had an authenticated Stripe
session.

The next action is interactive: the Product Owner must sign in through the
handed-off Chrome tab and confirm the proposed sandbox-only fixture in
`docs/STRIPE_SANDBOX_OPERATIONAL_PREFLIGHT_2026-08-13.md`. A reachable HTTPS
webhook also remains necessary for end-to-end event delivery; deployment and a
Stripe CLI installation are still separately gated.
