# Stripe sandbox operational proof — 2026-08-13

## Outcome

The bounded, synthetic Stripe lifecycle completed against `ShowVault Pro
sandbox`. No live-mode object, real person/customer/venue/payment data,
deployment, release, production configuration, or external Git mutation was
used.

The proof used the allowlisted sandbox offering, one synthetic Customer, one
card-only Checkout, one Subscription, its initial Invoice and Charge, the
configured hosted Customer Portal, signed loopback webhook forwarding, and an
isolated synthetic PostgreSQL database. Provider keys and webhook signing
secrets remained in macOS login Keychain and were not printed or written to
the repository.

## Successful lifecycle

- Checkout session
  `cs_test_b1TZzuEi95silsz3lxdbTvBNrpZrKbzWQo8j8hXm4SrVVXJ0Cs94nG3F0Q`
  completed in sandbox with the exact recurring and one-time Prices.
- Signed events returned HTTP 200 through the loopback-only final hop.
- The purchase attempt became `Completed`; the account binding used sandbox
  Customer `cus_V4D3JLukJ7Vuep`, Subscription
  `sub_1U44dXAHdGAkls09d7DhU88T`, and initial Invoice
  `in_1U44dVAHdGAkls09EcXAYBWb`.
- Current-state reconciliation activated both the recurring service and the
  perpetual license only after a signed event was processed.
- The hosted Portal exposed only the configured invoice history,
  payment-method update, and end-of-period cancellation behavior.
- Portal cancellation scheduled service end for September 13, 2026. Service
  remains `Active` through that current period.

The Portal Session itself was created through the authenticated Stripe CLI.
This proves the sandbox Portal configuration and Customer lifecycle, but it
does not replace the deterministic adapter test for restricted-key Portal
Session creation. The restricted runtime key was exercised by every
account-backed current-state reconciliation read.

## Dahlia scheduled-cancellation compatibility

The Portal exposed a second observed `2026-07-29.dahlia` response drift. An
end-of-period cancellation was represented by a non-null `cancel_at` equal to
the recurring item's `current_period_end`, while
`cancel_at_period_end` remained false. The original adapter therefore recorded
the first Portal event as `stale_noop`.

The adapter now recognizes the exact equality as end-of-period cancellation
and includes the raw optional cancellation timestamp in its provider revision.
It does not infer end-of-period behavior for a different timestamp. A focused
fixture asserts the observed shape. A subsequent signed event reconciled as
`projection_updated`, with the subscription still active through the period
and no billing attention.

## Fail-closed refund proof

The initial synthetic Charge `ch_3U44dVAHdGAkls093Ym1UFjb` was USD 2.00.

1. A USD 1.00 partial sandbox refund delivered `charge.refunded`, returned
   HTTP 200, revoked the perpetual license, and recorded
   `Attention / license_refund_ambiguous`. Recurring service remained active.
2. Refunding the remaining USD 1.00 delivered another signed event, returned
   HTTP 200, moved the license to `Refunded`, and resolved the open attention.
   Recurring service and the completed purchase attempt remained intact.

The final Charge is fully refunded in sandbox. No recovery data was deleted or
modified.

## Failed-payment and dispute boundaries

Stripe's official sandbox triggers created unrelated synthetic
`invoice.payment_failed` and `charge.dispute.created` fixtures. Both signed
deliveries returned HTTP 200. Because neither fixture resolved to the
allowlisted Checkout/Subscription, each receipt became
`Attention / state_unavailable` with no organization ID. They did not change
the ShowVault license, service, purchase attempt, or organization attention.

An account-linked dispute could not be attached to the already fully refunded
Charge. The organization-linked dispute-denial behavior remains covered by the
deterministic signed-event test; no stronger account-backed dispute claim is
made.

## Webhook-secret rotation

A new Stripe CLI signing secret was stored as a distinct Keychain item. The API
started with the new secret first and previous secret second, and accepted a
new-secret signed Portal event while the two-secret overlap was active. It was
then restarted with only the new active value and accepted the refund,
failed-payment, and dispute deliveries. No signing-secret value entered logs,
evidence, configuration, or Git.

Repeated macOS Keychain prompts made the final restart use the already-approved
single-secret helper. The additional Keychain item was preserved; no Keychain
entry or provider resource was destructively cleaned up.

## Final local/provider state

- Subscription: sandbox, `Active`, cancellation scheduled for September 13,
  2026.
- Perpetual license: `Refunded` after a full USD 2.00 sandbox refund.
- Recurring service: `Active` through September 13, 2026.
- Purchase attempt: one `Completed`.
- Organization billing attentions: zero open.
- Receipts: six event/outcome groups covering projection updates, stale
  fail-closed handling, refund attention/resolution, and unrelated
  state-unavailable fixtures.
- ShowVault API and Stripe listener: stopped.

## Local validation

- focused Stripe provider tests: 4 passed;
- complete API project: 50 passed;
- Entity Framework: no pending model changes;
- focused .NET whitespace verification: passed;
- `git diff --check`: passed; and
- tracked-diff Stripe secret-value scan: passed.

The earlier full compatibility gate also passed contracts 22, platform 23,
Agent 291, local engine 67, Flutter 32, Flutter analysis, and warning-free
Release builds.
