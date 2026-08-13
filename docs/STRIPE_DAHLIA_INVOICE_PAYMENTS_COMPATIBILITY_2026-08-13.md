# Stripe Dahlia Invoice Payments compatibility — 2026-08-13

## Decision

Accept the two preserved compatibility changes. They correct an account-backed
response-shape drift observed with Stripe API version `2026-07-29.dahlia`
without widening provider permissions, event scope, or the fail-closed state
model.

An ordinary initial Invoice retrieval did not embed a `payments` member. The
dedicated bounded Invoice Payments request by exact Invoice ID returned the
single PaymentIntent needed to locate the initial Charge. The adapter now uses
that endpoint, retains `limit=10`, rejects pagination, requires exactly one
distinct PaymentIntent, validates the latest Charge identifier, and keeps the
existing paid/refunded/disputed mapping.

The deterministic provider fixture now matches the observed response topology:
the Invoice contains its paid status and subscription parent, while Invoice
Payments is a separate response.

## Review

- The Invoice ID originates from the already selected and validated initial
  subscription Invoice.
- The value is escaped before inclusion in the query string.
- No event payload, Checkout redirect, metadata claim, or client value becomes
  entitlement authority.
- No raw provider response or secret is persisted or logged.
- Ambiguous or paginated payment results still return no Charge and therefore
  remain fail-closed.
- The change introduces no SDK or provider dependency.

No review blocker was found.

## Validation

- Stripe provider adapter tests: 4 passed as part of API validation.
- API: 50 passed.
- Agent contracts: 22 passed.
- Platform: 23 passed.
- Agent: 291 passed.
- Local engine: 67 passed.
- Flutter: 32 passed; analysis reported no issues.
- Entity Framework: no pending model changes.
- Local engine host, sync host, and Agent Release builds: zero warnings and
  zero errors.
- Focused .NET whitespace verification: passed.
- `git diff --check`: passed.
- Tracked-diff Stripe secret-value scan: passed.

The contracts, platform, and Agent test projects were restored before their
final runs so their test SDK build properties were present and the reported
test counts represent executed tests rather than a no-op invocation.

## Boundary

This local compatibility commit does not resume provider operations. It does
not create or mutate a Checkout Session, Portal Session, subscription, payment,
refund, dispute, key, webhook secret, event destination, deployment, live-mode
resource, or external Git ref.
