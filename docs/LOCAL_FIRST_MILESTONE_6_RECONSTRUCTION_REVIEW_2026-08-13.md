# Milestone 6 provider-billing reconstruction review — 2026-08-13

## Decision

Reconstruct one Stripe-hosted purchase and billing-management funnel against
milestone 5. Do not replay the broad historical account plan and do not build a
custom payment form, ShowVault staff console, or full customer website.

The desktop initiates only Owner-authorized, server-created hosted Checkout and
Billing Portal sessions. Redirects remain presentation. Verified durable event
receipt followed by current-state reconciliation is the sole provider path
into ShowVault's normalized commercial projections.

## Historical reconciliation

The historical architecture correctly separated the desktop, customer portal,
and staff Admin; one-time license from recurring subscription; provider facts
from entitlement; and passwords/payment details from ShowVault. It did not have
the current local-first recovery engine, hosted reservation semantics,
provider-independent projection, usage ledger, Owner plan endpoint, or
append-only audit model.

Current reconstruction retains its conceptual boundaries but replaces the
suggested all-at-once implementation order with a single funnel. Milestone 5
remains authority. Provider IDs and event processing live in a separate
server-only layer, and billing changes never delete or strand recovery data.

## Official-provider findings

Current Stripe documentation supports one subscription-mode Checkout Session
containing recurring and one-time Prices, with the one-time item limited to the
initial invoice. Fulfillment belongs behind payment-aware webhook handling, not
the success redirect. Billing Portal sessions are created server-side for an
existing Customer and return a short-lived URL.

Stripe requires signature verification over the unmodified raw webhook body.
It may deliver duplicate events and does not guarantee order. Therefore a
controller that directly mutates entitlement from parsed event JSON would be
unsafe. The plan requires verify-first minimal inbox durability, unique event
receipts, and reconciliation by retrieving current provider objects through a
bounded adapter.

## Current-system findings

Milestone 5 has exactly the right normalized seam but deliberately lacks
provider provenance. Adding Stripe IDs directly to public plan records or
letting the desktop submit Price/Customer IDs would contaminate that boundary.
Separate bindings can enforce one organization/provider/environment identity,
while the existing evaluator continues to decide hosted access.

The current Settings surface is Owner-aware and already renders normalized
plan/storage state. It can add two explicit external-browser actions without a
new customer app. There is no current ShowVault web portal; introducing a full
web authentication/session stack would materially broaden this first provider
slice.

## Lifecycle findings

Checkout completion can precede or accompany asynchronous payment and is not
proof that the initial license amount was paid. The one-time license stays
pending until reconciliation proves the exact allowlisted initial invoice paid.
Subscription status is independently normalized. Unknown or lossy provider
states must expand the normalized model or deny attention; they must not be
mislabelled active.

Milestone 5 permits past-due only with an explicit future grace deadline. Since
no grace duration is approved, the safe first provider mapping leaves grace
unset and denies new reservations. Cancellation, refund, and dispute likewise
change new-service eligibility but do not delete backups or block completion of
an already reserved hosted session.

## Security and privacy findings

Hosted Checkout and Billing Portal keep payment instruments outside ShowVault.
Provider raw payloads still contain unnecessary personal/financial fields, so
they may exist only transiently for verification and mapping. The durable inbox
stores a digest and identifiers, not raw JSON. Checkout/Portal URLs are bearer-
like short-lived capabilities and must not be stored or logged.

Keys and webhook secrets are distinct, server-only, environment-specific, and
absent from checked-in configuration. Disabled-by-default configuration and a
narrow synthetic adapter allow complete local proof without an account,
credential, Customer, or charge.

## Planning conclusion

The provider-specific implementation contract is ready for separate Product
Owner authorization. This review changed documentation only and read public
official documentation. It did not install a plugin/dependency, access a
Stripe account or API, create provider resources, use credentials or payment/
customer data, register a webhook, create a Checkout/Portal session, modify
application code or migrations, mutate cloud state, deploy, perform a native
action, change external Git state, or clean up destructively.
